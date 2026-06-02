using MongoDB.Driver;
using Microsoft.Extensions.Options;
using User.Models;

namespace UserService.Models
{
    public class MongoDBContext
    {
        private readonly IMongoDatabase _database;
        private readonly DBSettings _settings;

        public MongoDBContext(IOptions<DBSettings> settings)
        {
            _settings = settings.Value;
            var client = new MongoClient(_settings.ConnectionString);
            _database = client.GetDatabase(_settings.DatabaseName);
        }
        public IMongoCollection<User.Models.User> Users =>
            _database.GetCollection<User.Models.User>(_settings.CollectionName);
    }
}
