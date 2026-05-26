using System.Diagnostics.Metrics;

namespace FoodSelection.Metrics;

public class MetricService
{
    private readonly Counter<int> _productCreatedCounter;
    private readonly Histogram<double> _dbOperationDuration;
    private readonly Counter<int> _cacheRequestsCounter;

    public MetricService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("FoodSelection.API");

        _productCreatedCounter = meter.CreateCounter<int>("foodselection.products.created",
            description: "Количество созданных продуктов");

        _dbOperationDuration = meter.CreateHistogram<double>("foodselection.db.operation.duration",
            unit: "ms", description: "Длительность операций с MongoDB");

        _cacheRequestsCounter = meter.CreateCounter<int>("foodselection.cache.requests",
            description: "Статистика обращений к кэшу (попадания и промахи)");
    }

    public void ProductCreated(string category) =>
        _productCreatedCounter.Add(1, new KeyValuePair<string, object?>("category", category));

    public void RecordDbOperationDuration(double durationMs, string operation) =>
        _dbOperationDuration.Record(durationMs, new KeyValuePair<string, object?>("operation", operation));

    public void RecordCacheRequest(string cacheType, bool isHit) =>
        _cacheRequestsCounter.Add(1,
            new KeyValuePair<string, object?>("cache_type", cacheType),
            new KeyValuePair<string, object?>("status", isHit ? "hit" : "miss"));
}