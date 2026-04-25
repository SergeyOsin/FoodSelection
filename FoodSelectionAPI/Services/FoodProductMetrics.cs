using FoodSelection.Model;
using FoodSelection.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using FoodSelection.Data;
using System.Diagnostics;

namespace FoodSelection.Services;

public class FoodProductMetrics : IFoodProductMetrics
{
<<<<<<< HEAD
    private readonly Counter<int> _productCreatedCounter;
    private readonly Histogram<double> _dbOperationDuration;
=======
    private readonly IMongoCollection<FoodProduct> _foodProducts;
    private readonly GrafanService _FoodProductMetrics;
    MongoDbContext mongoDB;
>>>>>>> 9fe83ba (Некоторые изменения)

    public FoodProductMetrics(IOptions<MongoDbSettings> contextDB, GrafanService FoodProductMetrics)
    {
<<<<<<< HEAD
        var meter = meterFactory.Create("FoodSelection.API");
        _productCreatedCounter = meter.CreateCounter<int>("foodselection.products.created",
            description: "Количество созданных продуктов");
        _dbOperationDuration = meter.CreateHistogram<double>("foodselection.db.operation.duration",
            unit: "ms", description: "Длительность операций с MongoDB");
=======
        mongoDB = new MongoDbContext(contextDB);
        _foodProducts = mongoDB.FoodProducts;
        _FoodProductMetrics = FoodProductMetrics;
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
>>>>>>> 9fe83ba (Некоторые изменения)
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
        return product != null ? MapToResponseDto(product) : null;
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

        _FoodProductMetrics.ProductCreated(product.Category);
        _FoodProductMetrics.RecordDbOperationDuration(stopwatch.ElapsedMilliseconds, "insert_one");

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
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _foodProducts.DeleteOneAsync(p => p.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task DeleteAllAsync() =>
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