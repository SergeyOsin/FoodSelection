using FoodSelection.Model;
using FoodSelection.Models;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using FoodSelection.Data;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using FoodSelection.Metrics;

namespace FoodSelection.Services;

public class FoodProductService : IFoodProductService
{
    private readonly IMongoCollection<FoodProduct> _foodProducts;
    private readonly MetricService _foodProductMetrics;
    private readonly IDistributedCache _distributedCache;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FoodProductService(IOptions<MongoDbSettings> contextDB, MetricService foodProductMetrics,
        IDistributedCache distributedCache)
    {
        var mongoDB = new MongoDbContext(contextDB);
        _foodProducts = mongoDB.FoodProducts;
        _foodProductMetrics = foodProductMetrics;
        _distributedCache = distributedCache;
    }

    public async Task<List<FoodProductResponseDto>> GetAllAsync()
    {
        string cacheKey = "foodproducts:all";

        var cached = await _distributedCache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
            return JsonSerializer.Deserialize<List<FoodProductResponseDto>>(cached, JsonOptions) ?? [];

        var count = await _foodProducts.CountDocumentsAsync(_ => true);
        List<FoodProduct> products;

        if (count <= 1000)
            products = await _foodProducts.Find(_ => true).ToListAsync();
        else
            products = await _foodProducts.Aggregate().Sample(1000).As<FoodProduct>().ToListAsync();

        var result = products.Select(MapToResponseDto).ToList();

        await _distributedCache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result, JsonOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

        return result;
    }

    public async Task<List<FoodProductResponseDto>> FilterAsync(FoodProductFilterDto filter)
    {
        var cacheKey = $"foodproducts:filter:{JsonSerializer.Serialize(filter, JsonOptions)}";

        var cached = await _distributedCache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
            return JsonSerializer.Deserialize<List<FoodProductResponseDto>>(cached, JsonOptions) ?? [];

        var builder = Builders<FoodProduct>.Filter;
        var filters = new List<FilterDefinition<FoodProduct>>();

        if (!string.IsNullOrEmpty(filter.Category))
            filters.Add(builder.Eq(p => p.Category, filter.Category));

        if (filter.IsVegan.HasValue)
            filters.Add(builder.Eq(p => p.IsVegan, filter.IsVegan.Value));

        if (filter.IsVegetarian.HasValue)
            filters.Add(builder.Eq(p => p.IsVegetarian, filter.IsVegetarian.Value));

        if (filter.MinCalories.HasValue)
            filters.Add(builder.Gte(p => p.Calories, filter.MinCalories.Value));

        if (filter.MaxCalories.HasValue)
            filters.Add(builder.Lte(p => p.Calories, filter.MaxCalories.Value));

        if (filter.CreatedAfter.HasValue)
            filters.Add(builder.Gte(p => p.CreatedAt, filter.CreatedAfter.Value));

        if (filter.CreatedBefore.HasValue)
            filters.Add(builder.Lte(p => p.CreatedAt, filter.CreatedBefore.Value));

        var filterDefinition = filters.Count > 0 ? builder.And(filters) : builder.Empty;
        var products = await _foodProducts.Find(filterDefinition).ToListAsync();
        var result = products.Select(MapToResponseDto).ToList();

        await _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result, JsonOptions), 
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

        return result;
    }

    public async Task<FoodProductResponseDto?> GetByIdAsync(string id)
    {
        var cacheKey = $"foodproducts:id:{id}";

        var cached = await _distributedCache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
            return JsonSerializer.Deserialize<FoodProductResponseDto>(cached, JsonOptions);

        var product = await _foodProducts.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (product == null)
            return null;

        var result = MapToResponseDto(product);

        await _distributedCache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result, JsonOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(720)
            });

        return result;
    }

    public async Task<FoodProductResponseDto> CreateAsync(FoodProductCreateDto createDto)
    {
        var stopwatch = Stopwatch.StartNew();
        var product = new FoodProduct
        {
            Name = createDto.Name,
            Calories = createDto.Calories,
            Protein = createDto.Protein,
            Carbs = createDto.Carbs,
            Fats = createDto.Fats,
            Category = createDto.Category,
            IsVegan = createDto.IsVegan,
            IsVegetarian = createDto.IsVegetarian,
            CreatedAt = DateTime.UtcNow
        };

        await _foodProducts.InsertOneAsync(product);
        stopwatch.Stop();

        await InvalidateCacheAsync();

        _foodProductMetrics.ProductCreated(product.Category);
        _foodProductMetrics.RecordDbOperationDuration(stopwatch.ElapsedMilliseconds, "insert_one");

        return MapToResponseDto(product);
    }

    public async Task<bool> UpdateAsync(string id, FoodProductCreateDto updateDto)
    {
        var update = Builders<FoodProduct>.Update
            .Set(p => p.Name, updateDto.Name)
            .Set(p => p.Calories, updateDto.Calories)
            .Set(p => p.Protein, updateDto.Protein)
            .Set(p => p.Carbs, updateDto.Carbs)
            .Set(p => p.Fats, updateDto.Fats)
            .Set(p => p.Category, updateDto.Category)
            .Set(p => p.IsVegan, updateDto.IsVegan)
            .Set(p => p.IsVegetarian, updateDto.IsVegetarian);

        var result = await _foodProducts.UpdateOneAsync(p => p.Id == id, update);

        if (result.ModifiedCount > 0)
            await InvalidateCacheAsync();

        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _foodProducts.DeleteOneAsync(p => p.Id == id);

        if (result.DeletedCount > 0)
            await InvalidateCacheAsync();

        return result.DeletedCount > 0;
    }

    public async Task DeleteAllAsync()
    {
        await _foodProducts.DeleteManyAsync(_ => true);
        await InvalidateCacheAsync();
    }

    private async Task InvalidateCacheAsync()=>
        await _distributedCache.RemoveAsync("foodproducts:all");

    private FoodProductResponseDto MapToResponseDto(FoodProduct product) =>
        new()
        {
            Id = product.Id,
            Name = product.Name,
            Calories = product.Calories,
            Protein = product.Protein,
            Carbs = product.Carbs,
            Fats = product.Fats,
            Category = product.Category,
            IsVegan = product.IsVegan,
            IsVegetarian = product.IsVegetarian,
            CreatedAt = product.CreatedAt
        };
}