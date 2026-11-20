using Confluent.Kafka;
using FoodSelection.Data;
using FoodSelection.Metrics;
using MongoDB.Driver;
using System.Text.Json;

namespace FoodSelection.Models;

public class ConfirmationRequestPublisher : BackgroundService
{
    private readonly MongoDbContext _mongoDbContext;
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<ConfirmationRequestPublisher> _logger;

    public ConfirmationRequestPublisher(
        MongoDbContext mongoDbContext,
        IProducer<Null, string> producer,
        ILogger<ConfirmationRequestPublisher> logger)
    {
        _mongoDbContext = mongoDbContext;
        _producer = producer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pendingFilter = Builders<FoodSelection.Model.FoodProduct>.Filter.And(
                    Builders<FoodSelection.Model.FoodProduct>.Filter.Eq(product => product.Status, "Pending"),
                    Builders<FoodSelection.Model.FoodProduct>.Filter.Ne(
                        product => product.ConfirmationRequestPublished,
                        true));
                var pendingRequests = await _mongoDbContext.FoodProducts
                    .Find(pendingFilter)
                    .Limit(100)
                    .ToListAsync(stoppingToken);

                foreach (var product in pendingRequests)
                {
                    if (string.IsNullOrWhiteSpace(product.Id))
                        continue;

                    var message = JsonSerializer.Serialize(new
                    {
                        Id = product.Id,
                        UserID = product.UserID
                    });

                    await _producer.ProduceAsync(
                        "object-created-topic",
                        new Message<Null, string> { Value = message },
                        stoppingToken);

                    var unpublishedFilter = Builders<FoodSelection.Model.FoodProduct>.Filter.And(
                        Builders<FoodSelection.Model.FoodProduct>.Filter.Eq(item => item.Id, product.Id),
                        Builders<FoodSelection.Model.FoodProduct>.Filter.Ne(
                            item => item.ConfirmationRequestPublished,
                            true));
                    await _mongoDbContext.FoodProducts.UpdateOneAsync(
                        unpublishedFilter,
                        Builders<FoodSelection.Model.FoodProduct>.Update
                            .Set(item => item.ConfirmationRequestPublished, true),
                        cancellationToken: stoppingToken);

                    ConfirmationMetrics.Record("request_published");
                    _logger.LogInformation(
                        "Запрос подтверждения объекта {ObjectId} доставлен в Kafka",
                        product.Id);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось отправить запросы подтверждения в Kafka; повторим попытку");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}
