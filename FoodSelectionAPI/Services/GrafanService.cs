using System.Diagnostics.Metrics;

namespace FoodSelection.Services;

public class GrafanService
{
    private readonly Counter<int> _productCreatedCounter;
    private readonly Histogram<double> _dbOperationDuration;

    public GrafanService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("FoodSelection.API");
        _productCreatedCounter = meter.CreateCounter<int>("foodselection.products.created",
            description: "Количество созданных продуктов");
        _dbOperationDuration = meter.CreateHistogram<double>("foodselection.db.operation.duration",
            unit: "ms", description: "Длительность операций с MongoDB");
    }

    public void ProductCreated(string category)=>
        _productCreatedCounter.Add(1, new KeyValuePair<string, object?>("category", category));


    public void RecordDbOperationDuration(double durationMs, string operation) =>
        _dbOperationDuration.Record(durationMs, new KeyValuePair<string, object?>("operation", operation));
}