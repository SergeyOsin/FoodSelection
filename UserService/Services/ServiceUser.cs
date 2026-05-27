using MongoDB.Driver;
namespace User.Services;

using User.Models;
public class ServiceUser
{
    private readonly IMongoCollection<User> _users;

    public ServiceUser(IConfiguration configuration)
    {
        var client = new MongoClient(configuration.GetConnectionString("MongoDb"));

        var database = client.GetDatabase("UserDb");

        _users = database.GetCollection<User>("Users");
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _users.Find(_ => true).ToListAsync();
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        return await _users.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<User> CreateAsync(User user)
    {
        user.RegisteredObjects = 0;

        await _users.InsertOneAsync(user);

        return user;
    }

    public async Task UpdateAsync(string id, string name)
    {
        var update = Builders<User>.Update.Set(x => x.Name, name);

        await _users.UpdateOneAsync(x => x.Id == id, update);
    }

    public async Task DeleteAsync(string id)
    {
        await _users.DeleteOneAsync(x => x.Id == id);
    }

    public async Task IncrementRegisteredObjects(string userId)
    {
        var update = Builders<User>.Update.Inc(x => x.RegisteredObjects, 1);

        await _users.UpdateOneAsync(x => x.Id == userId, update);
    }
}