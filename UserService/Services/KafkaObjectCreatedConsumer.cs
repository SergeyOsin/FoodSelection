using Confluent.Kafka;
using MongoDB.Driver;
using System.Text.Json;


namespace User.Kafka;

using User.Models;
public class KafkaObjectCreatedConsumer : BackgroundService
{
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IProducer<Null, string> _producer;

    public KafkaObjectCreatedConsumer(IConfiguration configuration)
    {
        var mongoClient = new MongoClient(
            configuration.GetConnectionString("MongoDb"));

        var db = mongoClient.GetDatabase("UserDb");
        _usersCollection = db.GetCollection<User>("Users");

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "kafka:9092"
        };

        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            GroupId = "user-service-group",
            BootstrapServers = "kafka:9092",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer =
            new ConsumerBuilder<Ignore, string>(consumerConfig).Build();

        consumer.Subscribe("object-created-topic");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);

                var message =
                    JsonSerializer.Deserialize<ObjectCreated>(
                        result.Message.Value);

                if (message == null)
                    continue;

                var filter =
                    Builders<User>.Filter.Eq(x => x.Id, message.UserId);

                var update =
                    Builders<User>.Update.Inc(x => x.RegisteredObjects, 1);

                await _usersCollection.UpdateOneAsync(
                    filter,
                    update,
                    cancellationToken: stoppingToken);

                var replyEvent =
                    new ObjectConfirmed(
                        message.ObjectId,
                        DateTime.UtcNow.ToString("O"));

                var json =
                    JsonSerializer.Serialize(replyEvent);

                await _producer.ProduceAsync(
                    "object-confirmed-topic",
                    new Message<Null, string> { Value = json },
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            consumer.Close();
        }
    }
}