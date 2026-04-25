using FoodSelection.Model;
using FoodSelection.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using FoodSelection.Data;
using System.Diagnostics;

namespace FoodSelection.Services;

public class FoodProductMetrics
{
    private readonly Counter<int> _productCreatedCounter;
    private readonly Histogram<double> _dbOperationDuration;
    private readonly Gauge<double> _gauge;

    public FoodProductMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("FoodSelection.API");
        _productCreatedCounter = meter.CreateCounter<int>("foodselection.products.created",
            description: "Количество созданных продуктов");
        _dbOperationDuration = meter.CreateHistogram<double>("foodselection.db.operation.duration",
            unit: "ms", description: "Длительность операций с MongoDB");
        _gauge = meter.CreateGauge<double>(
            name: "time",
            unit: "seconds",
            description: "Секунды");
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