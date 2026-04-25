using System.Diagnostics.Metrics;

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

    public void ProductCreated(string category)=>
        _productCreatedCounter.Add(1, new KeyValuePair<string, object?>("category", category));


    public void RecordDbOperationDuration(double durationMs, string operation) =>
        _dbOperationDuration.Record(durationMs, new KeyValuePair<string, object?>("operation", operation));
}