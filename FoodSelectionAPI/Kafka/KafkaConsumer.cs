using Confluent.Kafka;
using FoodSelection.Metrics;
using System.Text.Json;
using FoodSelection.Services;
using Microsoft.Extensions.Configuration; 
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Extensions.Hosting; 
using Microsoft.Extensions.Logging; 
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodSelection.Models;

public class KafkaConsumer : BackgroundService
{
    private readonly ILogger<KafkaConsumer> _logger;
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public KafkaConsumer(ILogger<KafkaConsumer> logger, IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "kafka:29092";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "foodselection-confirmation-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = true,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<Ignore, string>(config).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        
        _consumer.Subscribe("confirmation-topic");
        _logger.LogInformation("Подписка на confirmation-topic успешна");

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

                ConfirmationMessage? confirmation;
                try
                {
                    confirmation = JsonSerializer.Deserialize<ConfirmationMessage>(
                        consumeResult.Message.Value,
                        JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Получено некорректное JSON-сообщение подтверждения");
                    _consumer.Commit(consumeResult);
                    continue;
                }

                if (confirmation is null ||
                    string.IsNullOrWhiteSpace(confirmation.ObjectId) ||
                    confirmation.ConfirmationTime == default)
                {
                    _logger.LogWarning("Получено некорректное сообщение подтверждения: {Message}", consumeResult.Message.Value);
                    _consumer.Commit(consumeResult);
                    continue;
                }

                _logger.LogInformation("Получено подтверждение объекта {ObjectId}", confirmation.ObjectId);
                using var scope = _scopeFactory.CreateScope();
                var foodProductService = scope.ServiceProvider.GetRequiredService<IFoodProductService>();
                var confirmed = await foodProductService.ConfirmAsync(
                    confirmation.ObjectId,
                    confirmation.ConfirmationTime);

                if (confirmed)
                {
                    ConfirmationMetrics.Record("confirmation_received");
                    _logger.LogInformation("Сохранено подтверждение объекта {ObjectId}", confirmation.ObjectId);
                }
                else
                    _logger.LogWarning("Объект с ID {ObjectId} не найден", confirmation.ObjectId);

                _consumer.Commit(consumeResult);
            }
            catch (ConsumeException ex)
            {
                if (ex.Error.Code == Confluent.Kafka.ErrorCode.UnknownTopicOrPart)
                {
                    _logger.LogWarning("Kafka: Топик confirmation-topic еще не готов. Ждем...");
                }
                else
                {
                    _logger.LogWarning($"Kafka некритичная ошибка: {ex.Error.Reason}");
                }
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Критическая ошибка при обработке подтверждения");
                if (consumeResult is not null)
                    _consumer.Seek(consumeResult.TopicPartitionOffset);
                await Task.Delay(1000, stoppingToken); 
            }
        }
    }

    public override void Dispose()
    {
        _consumer?.Close(); 
        _consumer?.Dispose();
        base.Dispose();
    }
}

public class ConfirmationMessage
{
    public string ObjectId { get; set; } = string.Empty;
    public DateTimeOffset ConfirmationTime { get; set; }
}