using Prometheus;

namespace UserService.Services;

public static class ConfirmationMetrics
{
    private static readonly Counter Events = Prometheus.Metrics.CreateCounter(
        "user_service_confirmation_events_total",
        "Этапы обработки запросов подтверждения",
        new CounterConfiguration { LabelNames = new[] { "stage" } });

    public static void Record(string stage) => Events.WithLabels(stage).Inc();
}
