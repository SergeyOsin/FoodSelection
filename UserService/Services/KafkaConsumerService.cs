using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using User.Models;

namespace UserService.Services;

public class KafkaConsumerService : BackgroundService
{
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly IProducer<Null, string> _producer;
    private readonly IServiceScopeFactory _scopeFactory;

    public KafkaConsumerService(
        ILogger<KafkaConsumerService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = "kafka:29092",
            GroupId = "user-service-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = true
        };

        _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig)
            .Build();

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = "kafka:29092"
        };

        _producer = new ProducerBuilder<Null, string>(producerConfig)
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        _consumer.Subscribe("object-created-topic");
        _logger.LogInformation("Подписка на object-created-topic успешна");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(
                    TimeSpan.FromMilliseconds(500));

                if (consumeResult == null)
                {
                    await Task.Delay(100, stoppingToken);
                    continue;
                }

                _logger.LogInformation(
                    "Получено сообщение: {Message}",
                    consumeResult.Message.Value);

                var message =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(
                        consumeResult.Message.Value);

                if (message == null)
                {
                    _logger.LogWarning("Не удалось десериализовать сообщение");
                    continue;
                }

                if (!message.TryGetValue("ObjectId", out var objectId))
                {
                    _logger.LogWarning("Сообщение не содержит ObjectId");
                    continue;
                }

                if (!message.TryGetValue("UserId", out var userIdString))
                {
                    _logger.LogWarning("Сообщение не содержит UserId");
                    continue;
                }

                if (!Guid.TryParse(userIdString, out var userId))
                {
                    _logger.LogWarning(
                        "Некорректный формат UserId: {UserId}",
                        userIdString);

                    continue;
                }

                using var scope = _scopeFactory.CreateScope();

                var userService =
                    scope.ServiceProvider.GetRequiredService<ServiceUser>();

                var user = await userService.GetByIdAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning(
                        "Пользователь с ID {UserId} не найден",
                        userId);

                    continue;
                }

                await userService.IncrementRegisteredObjects(userId);

                _logger.LogInformation(
                    "Инкрементирован RegisteredObjects для пользователя {UserId}",
                    userId);

                var response = new
                {
                    ObjectId = objectId,
                    ConfirmationTime = DateTime.UtcNow.ToString("o")
                };

                var serializedResponse =
                    JsonSerializer.Serialize(response);

                await _producer.ProduceAsync(
                    "confirmation-topic",
                    new Message<Null, string>
                    {
                        Value = serializedResponse
                    },
                    stoppingToken);

                _logger.LogInformation(
                    "Отправлено подтверждение для объекта {ObjectId}",
                    objectId);
            }
            catch (ConsumeException ex)
            {
                if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                {
                    _logger.LogWarning(
                        "Kafka: Топик object-created-topic еще не готов. Ждем...");
                }
                else
                {
                    _logger.LogWarning(
                        "Kafka ошибка: {Reason}",
                        ex.Error.Reason);
                }

                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Ошибка при обработке сообщения");

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