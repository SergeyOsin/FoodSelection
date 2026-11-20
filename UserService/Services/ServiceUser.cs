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
            Name = user.Name!,
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

    public async Task RegisterObject(Guid userId, string objectId)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(x => x.Id, userId),
            Builders<User>.Filter.Where(x => !x.RegisteredObjectIds.Contains(objectId)));
        var update = Builders<User>.Update
            .Inc(x => x.RegisteredObjects, 1)
            .AddToSet(x => x.RegisteredObjectIds, objectId);

        await _users.UpdateOneAsync(filter, update);
    }

    public async Task UnregisterObject(Guid userId, string objectId)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(x => x.Id, userId),
            Builders<User>.Filter.AnyEq("RegisteredObjectIds", objectId));
        var update = Builders<User>.Update
            .Inc(x => x.RegisteredObjects, -1)
            .Pull("RegisteredObjectIds", objectId);

        await _users.UpdateOneAsync(filter, update);
    }
    
    public async Task ResetAllRegisteredObjectsAsync()
    {
        var update = Builders<User>.Update
            .Set(x => x.RegisteredObjects, 0)
            .Set(x => x.RegisteredObjectIds, new List<string>());
    
        await _users.UpdateManyAsync(Builders<User>.Filter.Empty, update);
    }
}