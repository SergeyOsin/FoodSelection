using Confluent.Kafka;
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
            AllowAutoCreateTopics = true 
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
            try
            {
                var consumeResult = _consumer.Consume(TimeSpan.FromMilliseconds(500));
                
                if (consumeResult == null)
                {
                    await Task.Delay(100, stoppingToken);
                    continue;
                }

                _logger.LogInformation($"Получено подтверждение: {consumeResult.Message.Value}");
                var confirmation = JsonSerializer.Deserialize<ConfirmationMessage>(consumeResult.Message.Value);
                if (confirmation != null)
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var eventsService = scope.ServiceProvider.GetRequiredService<FoodProductService>();
                        var ev = await eventsService.GetByIdAsync(confirmation.ObjectId);
                        if (ev != null)
                        {
                            ev.CreatedAt = DateTime.Parse(confirmation.ConfirmationTime);
                            await eventsService.UpdateAsync(confirmation.ObjectId, ev);
                            _logger.LogInformation($"Обновлён статус для объекта {confirmation.ObjectId}");
                        }
                        else
                        {
                            _logger.LogWarning($"Объект с ID {confirmation.ObjectId} не найден");
                        }
                    }
                }
            }
            // ВАЖНО: Ловим специфичную ошибку Kafka!
            catch (ConsumeException ex)
            {
                if (ex.Error.Code == Confluent.Kafka.ErrorCode.UnknownTopicOrPart)
                {
                    // Топик еще создается, это нормально. Не пишем Error, просто ждем.
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
    public string ConfirmationTime { get; set; } = string.Empty;
}