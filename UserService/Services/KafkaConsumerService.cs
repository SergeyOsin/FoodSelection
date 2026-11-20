using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
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

    public KafkaConsumerService(
        ILogger<KafkaConsumerService> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;

        _consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "kafka:29092",
            GroupId = "user-service-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = true,
            EnableAutoCommit = false
        }).Build();

        _producer = new ProducerBuilder<Null, string>(new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "kafka:29092",
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(new[] { "object-created-topic", "object-deleted-topic", "all-objects-deleted-topic" });
        _logger.LogInformation("Подписка на топики успешна");

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<Ignore, string>? consumeResult = null;
            try
            {
                consumeResult = _consumer.Consume(TimeSpan.FromMilliseconds(500));
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
                    _consumer.Commit(consumeResult);
                    continue;
                }

                var kafkaEvent = JsonSerializer.Deserialize<KafkaEvent>(consumeResult.Message.Value, _jsonOptions);
                if (kafkaEvent == null ||
                    string.IsNullOrWhiteSpace(kafkaEvent.Id) ||
                    string.IsNullOrWhiteSpace(kafkaEvent.UserID))
                {
                    _logger.LogWarning("Получено некорректное событие из {Topic}", topic);
                    _consumer.Commit(consumeResult);
                    continue;
                }

                if (!Guid.TryParse(kafkaEvent.UserID, out var userId))
                {
                    _logger.LogWarning("Некорректный UserID в событии {ObjectId}", kafkaEvent.Id);
                    _consumer.Commit(consumeResult);
                    continue;
                }

                var user = await userService.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Пользователь {UserId} из события не найден", userId);
                    _consumer.Seek(consumeResult.TopicPartitionOffset);
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                if (topic == "object-created-topic")
                {
                    await userService.RegisterObject(userId, kafkaEvent.Id);
                    ConfirmationMetrics.Record("request_processed");

                    var response = new
                    {
                        ObjectId = kafkaEvent.Id,
                        ConfirmationTime = DateTimeOffset.UtcNow.ToString("O")
                    };
                    await _producer.ProduceAsync(
                        "confirmation-topic",
                        new Message<Null, string>
                        {
                            Value = JsonSerializer.Serialize(response)
                        },
                        stoppingToken);
                    ConfirmationMetrics.Record("response_published");
                }
                else if (topic == "object-deleted-topic")
                    await userService.UnregisterObject(userId, kafkaEvent.Id);

                _consumer.Commit(consumeResult);
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
                if (consumeResult is not null)
                    _consumer.Seek(consumeResult.TopicPartitionOffset);
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