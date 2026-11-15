using MongoDB.Driver;
using Microsoft.Extensions.Options;
using UserService.Models;

namespace User.Models;

public class ServiceUser
{
    private readonly IMongoCollection<User> _users;

    public ServiceUser(IOptions<DBSettings> dataSet)
    {
        var client = new MongoDBContext(dataSet);
        _users = client.Users;
    }

    public async Task<List<User>> GetAllAsync() =>
        await _users.Find(_ => true).ToListAsync();

    public async Task<User?> GetByIdAsync(Guid id) =>
        await _users.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task<Guid> CreateAsync(CreateDTOUser user)
    {
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Name = user?.Name ?? "Unknown",
            RegisteredObjects = 0
        };

        await _users.InsertOneAsync(newUser);
        return newUser.Id;
    }

    public async Task UpdateAsync(Guid id, string newName)
    {
        var update = Builders<User>.Update
            .Set(x => x.Name, newName);

        await _users.UpdateOneAsync(x => x.Id == id, update);
    }

    public async Task DeleteAsync(Guid id) =>
        await _users.DeleteOneAsync(x => x.Id == id);

    public async Task IncrementRegisteredObjects(Guid userId)
    {
        var update = Builders<User>.Update
            .Inc(x => x.RegisteredObjects, 1);

        await _users.UpdateOneAsync(
            x => x.Id == userId,
            update);
    }

    public async Task DecrementRegisteredObjects(Guid userId)
    {
        var delete = Builders<User>.Update.Inc(x=>x.RegisteredObjects, -1);
        await _users.UpdateOneAsync(x => x.Id == userId, delete);
    }
    
    public async Task ResetAllRegisteredObjectsAsync()
    {
        var filter = Builders<User>.Filter.Gt(x => x.RegisteredObjects, 0);
        var update = Builders<User>.Update.Set(x => x.RegisteredObjects, 0);
    
        await _users.UpdateManyAsync(filter, update);
    }
}