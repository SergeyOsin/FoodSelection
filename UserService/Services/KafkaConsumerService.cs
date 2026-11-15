using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using User.Models;

namespace UserService.Services;

public class KafkaEvent
{
    public string Id { get; set; } = string.Empty;
    public string UserID { get; set; } = string.Empty;
}

public class KafkaConsumerService : BackgroundService
{
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly IProducer<Null, string> _producer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public KafkaConsumerService(ILogger<KafkaConsumerService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;

        _consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = "kafka:29092",
            GroupId = "user-service-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = true
        }).Build();

        _producer = new ProducerBuilder<Null, string>(new ProducerConfig
        {
            BootstrapServers = "kafka:29092"
        }).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(new[] { "object-created-topic", "object-deleted-topic", "all-objects-deleted-topic" });
        _logger.LogInformation("Подписка на топики успешна");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(TimeSpan.FromMilliseconds(500));
                if (consumeResult == null)
                {
                    await Task.Delay(100, stoppingToken);
                    continue;
                }

                var topic = consumeResult.Topic;

                using var scope = _scopeFactory.CreateScope();
                var userService = scope.ServiceProvider.GetRequiredService<ServiceUser>();

                if (topic == "all-objects-deleted-topic")
                {
                    await userService.ResetAllRegisteredObjectsAsync();
                    _logger.LogInformation("Обнулены все RegisteredObjects");
                    continue;
                }

                var kafkaEvent = JsonSerializer.Deserialize<KafkaEvent>(consumeResult.Message.Value, _jsonOptions);
                if (kafkaEvent == null || string.IsNullOrEmpty(kafkaEvent.UserID))
                    continue;

                if (!Guid.TryParse(kafkaEvent.UserID, out var userId))
                    continue;

                var user = await userService.GetByIdAsync(userId);
                if (user == null)
                    continue;

                if (topic == "object-created-topic")
                    await userService.IncrementRegisteredObjects(userId);
                else if (topic == "object-deleted-topic")
                    await userService.DecrementRegisteredObjects(userId);

                var response = new { ObjectId = kafkaEvent.Id, ConfirmationTime = DateTime.UtcNow.ToString("o") };
                await _producer.ProduceAsync("confirmation-topic",
                    new Message<Null, string> { Value = JsonSerializer.Serialize(response) }, stoppingToken);
            }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }


    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
        _producer.Dispose();
        base.Dispose();
    }
}