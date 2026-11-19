using System.Security.Cryptography;
using AuthService.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AuthService.Services;

public class AuthUserStore
{
    private const int PasswordIterations = 210_000;
    private readonly IMongoCollection<AuthUser> _users;

    public AuthUserStore(IOptions<AuthDatabaseSettings> options)
    {
        var settings = options.Value;
        var database = new MongoClient(settings.ConnectionString).GetDatabase(settings.DatabaseName);
        _users = database.GetCollection<AuthUser>(settings.CollectionName);
        _users.Indexes.CreateOne(new CreateIndexModel<AuthUser>(
            Builders<AuthUser>.IndexKeys.Ascending(user => user.Username),
            new CreateIndexOptions<AuthUser>
            {
                Unique = true,
                PartialFilterExpression = Builders<AuthUser>.Filter.Type(
                    user => user.Username,
                    MongoDB.Bson.BsonType.String)
            }));
    }

    public async Task<AuthUser?> FindByUsernameAsync(string username) =>
        await _users.Find(user => user.Username == NormalizeUsername(username)).FirstOrDefaultAsync();

    public async Task<AuthUser?> RegisterAsync(RegisterRequest request)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var passwordHash = Rfc2898DeriveBytes.Pbkdf2(
            request.Password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            32);
        var user = new AuthUser
        {
            Id = Guid.NewGuid(),
            Username = NormalizeUsername(request.Username),
            Name = request.Name.Trim(),
            PasswordHash = Convert.ToBase64String(passwordHash),
            PasswordSalt = Convert.ToBase64String(salt)
        };

        try
        {
            await _users.InsertOneAsync(user);
            return user;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Code == 11000)
        {
            return null;
        }
    }

    public static bool VerifyPassword(string password, AuthUser user)
    {
        try
        {
            var salt = Convert.FromBase64String(user.PasswordSalt);
            var expectedHash = Convert.FromBase64String(user.PasswordHash);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                PasswordIterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();
}

public class AuthDatabaseSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
}
