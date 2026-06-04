using Confluent.Kafka;
using FoodSelection.Data;
using FoodSelection.Metrics;
using FoodSelection.Model;
using FoodSelection.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Diagnostics;
using System.Text.Json;

namespace FoodSelection.Services;

public class FoodProductService : IFoodProductService
{
    private readonly IMongoCollection<FoodProduct> _foodProducts;
    private readonly MetricService _foodProductMetrics;

    public FoodProductService(IOptions<MongoDbSettings> contextDB, MetricService foodProductMetrics)
    {
        var mongoDB = new MongoDbContext(contextDB);
        _foodProducts = mongoDB.FoodProducts;
        _foodProductMetrics = foodProductMetrics;
    }

    public async Task<List<FoodProductResponseDto>> GetAllAsync()
    {
        var count = await _foodProducts.CountDocumentsAsync(_ => true);
        List<FoodProduct> products;

        if (count <= 1000)
            products = await _foodProducts.Find(_ => true).ToListAsync();
        else
            products = await _foodProducts.Aggregate().Sample(1000).As<FoodProduct>().ToListAsync();

        return products.Select(MapToResponseDto).ToList();
    }

    public async Task<List<FoodProductResponseDto>> FilterAsync(FoodProductFilterDto filter)
    {
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

        return products.Select(MapToResponseDto).ToList();
    }

    public async Task<FoodProductResponseDto?> GetByIdAsync(string id)
    {
        var product = await _foodProducts.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (product == null)
            return null;

        return MapToResponseDto(product);
    }

    public async Task<FoodProductResponseDto> CreateAsync(FoodProductCreateDto createDto)
    {
        var product = new FoodProduct
        {
            Name = createDto.Name,
            UserID=createDto.UserID,
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

        var config = new ProducerConfig { BootstrapServers = "kafka:9092" };
        using var producer = new ProducerBuilder<Null, string>(config).Build();
        var json = JsonSerializer.Serialize(product);

        await producer.ProduceAsync("object-created", new Message<Null, string> { Value = json });

        return MapToResponseDto(product);
    }

    public async Task<bool> UpdateAsync(string id, FoodProductResponseDto updateDto)
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
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _foodProducts.DeleteOneAsync(p => p.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task DeleteAllAsync()=>
        await _foodProducts.DeleteManyAsync(_ => true);

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