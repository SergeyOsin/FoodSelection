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

    public async Task UpdateAsync(Guid id, User newUser)
    {
        newUser.Id = id;

        await _users.ReplaceOneAsync(
            x => x.Id == id,
            newUser);
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
}