using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace User.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        [BsonElement("Name")]
        public string Name { get; set; } = string.Empty;
        public int RegisteredObjects { get; set; } = 0;
    }
}
