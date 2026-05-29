using Confluent.Kafka;
using System.Text.Json;
using User.Models;
namespace UserService.Services;

public class KafkaConsumerService : BackgroundService
{
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly IProducer<Null, string> _producer;
    private readonly IServiceScopeFactory _scopeFactory;

    public KafkaConsumerService(ILogger<KafkaConsumerService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = "kafka:9092",
            GroupId = "user-service-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
        _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        var producerConfig = new ProducerConfig { BootstrapServers = "kafka:9092" };


       
        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _consumer.Subscribe("object-created-topic");
                var consumeResult = _consumer.Consume(TimeSpan.FromMilliseconds(500));
                if (consumeResult == null)
                {
                    await Task.Delay(100, stoppingToken);
                    continue;
                }

                _logger.LogInformation($"Получено сообщение: {consumeResult.Message.Value}");
                var message = JsonSerializer.Deserialize<Dictionary<string, string>>(consumeResult.Message.Value);
                if (message != null && message.TryGetValue("ObjectId", out var objectId) && message.TryGetValue("UserId", out var userId))
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var userService = scope.ServiceProvider.GetRequiredService<ServiceUser>();
                        var user = await userService.GetByIdAsync(userId);
                        if (user != null)
                        {
                            await userService.IncrementRegisteredObjects(userId);
                            _logger.LogInformation($"Инкрементирован RegisteredObjects для пользователя {userId}");

                            var response = new
                            {
                                ObjectId = objectId,
                                ConfirmationTime = DateTime.UtcNow.ToString("o")
                            };
                            var serializedResponse = JsonSerializer.Serialize(response);
                            await _producer.ProduceAsync("confirmation-topic", new Message<Null, string> { Value = serializedResponse }, stoppingToken);
                            _logger.LogInformation($"Отправлено подтверждение для объекта {objectId}");
                        }
                        else
                        {
                            _logger.LogWarning($"Пользователь с ID {userId} не найден");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Сообщение не содержит ObjectId или UserId");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке сообщения");
                await Task.Delay(500, stoppingToken);
            }
        }
    }

    public override void Dispose()
    {
        _consumer?.Close();
        _consumer?.Dispose();
        _producer?.Dispose();
        base.Dispose();
    }
}