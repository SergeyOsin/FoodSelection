using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthService.Models;

public class AuthUser
{
    [BsonId]
    public Guid Id { get; set; }

    [BsonElement("Name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("Username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("PasswordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("PasswordSalt")]
    public string PasswordSalt { get; set; } = string.Empty;

    [BsonElement("RegisteredObjects")]
    public int RegisteredObjects { get; set; }
}
