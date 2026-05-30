using MongoDB.Driver;
using Microsoft.Extensions.Options;
using User.Models;
using UserService.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace User.Models;
public class ServiceUser
{
    private readonly IMongoCollection<User> _users;

    public ServiceUser(IOptions<DataBase>dataSet)
    {
        var client = new MongoClient(dataSet.Value.ConnectionString);
        var database = client.GetDatabase(dataSet.Value.DatabaseName);
        _users = database.GetCollection<User>(dataSet.Value.CollectionName);
    }

    public async Task<List<User>> GetAllAsync()=> await _users.Find(_ => true).ToListAsync();

    public async Task<User?> GetByIdAsync(string id) => await _users.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task CreateAsync(CreateDTOUser user)
    {
        var newUser = new User
        {
            Name = user?.Name ?? "Unknown",
            RegisteredObjects = 0
        };
        await _users.InsertOneAsync(newUser);
    }

    public async Task UpdateAsync(string id, User NewUser)=>
        await _users.ReplaceOneAsync(x => x.Id == id, NewUser);

    public async Task DeleteAsync(string id)=> await _users.DeleteOneAsync(x => x.Id == id);

    public async Task IncrementRegisteredObjects(string userId)
    {
        var update = Builders<User>.Update.Inc(x => x.RegisteredObjects, 1);
        await _users.UpdateOneAsync(x => x.Id == userId, update);
    }
}