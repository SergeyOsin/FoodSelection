using Confluent.Kafka;
using FoodSelection.Data;
using FoodSelection.Model;
using MongoDB.Driver;
using System.Text.Json;

namespace FoodSelection.Kafka
{
    public class KafkaConsumer: BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public KafkaConsumer(IServiceProvider serviceProvider)=>_serviceProvider= serviceProvider;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var config = new ConsumerConfig
            {
                GroupId = "new-group",
                BootstrapServers = "kafka:9092",
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            consumer.Subscribe("имя-топика");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var Result = consumer.Consume(stoppingToken);
                    var messageJSON = JsonSerializer.Deserialize<ObjectConfirmed>(Result.Message.Value);
                    if (messageJSON != null)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
                        var filter = Builders<FoodProduct>.Filter.Eq(p => p.Id, messageJSON.ObjectId);
                        var update = Builders<FoodProduct>.Update.Set(p => p.Status, $"Confirmed");

                        await dbContext.FoodProducts.UpdateOneAsync(filter, update, cancellationToken: stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) { consumer.Close(); }
        }
    }
}
