using Confluent.Kafka;
using System.Text.Json;

namespace FoodSelection.Services;

public class KafkaProducer
{
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        var config = new ProducerConfig { 
            BootstrapServers = "kafka:9092" 
            
        };
        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    public async Task ProduceAsync(string topic, object message)
    {
        var serialized = JsonSerializer.Serialize(message);
        try
        {
            var delivery = await _producer.ProduceAsync(topic, new Message<Null, string> { Value = serialized });
            _logger.LogInformation($"Сообщение в топике {topic}: {serialized}");
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(ex, "Ошибка отправки");
            throw;
        }
    }
}
