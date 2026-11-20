using Prometheus;

namespace FoodSelection.Metrics;

public static class ConfirmationMetrics
{
    private static readonly Counter Events = Prometheus.Metrics.CreateCounter(
        "foodselection_confirmation_events_total",
        "Этапы подтверждения добавления объектов",
        new CounterConfiguration { LabelNames = new[] { "stage" } });

    public static void Record(string stage) => Events.WithLabels(stage).Inc();
}
