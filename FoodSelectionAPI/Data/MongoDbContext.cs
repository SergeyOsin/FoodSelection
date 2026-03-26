using Microsoft.Extensions.Options;
using MongoDB.Driver;
using FoodSelection.Model;
using FoodSelection.Models;

namespace FoodSelection.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;
        private readonly MongoDbSettings _settings;

        public MongoDbContext(IOptions<MongoDbSettings> settings)
        {
            _settings = settings.Value;
            var client = new MongoClient(_settings.ConnectionString);
            _database = client.GetDatabase(_settings.DatabaseName);
        }

        public IMongoCollection<FoodProduct> FoodProducts =>
            _database.GetCollection<FoodProduct>(_settings.CollectionName);
    }
}