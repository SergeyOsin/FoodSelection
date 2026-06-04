using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace User.Models
{
    public class User
    {
        [BsonId]
        public Guid Id { get; set; }
        [BsonElement("Name")]
        public string Name { get; set; } = string.Empty;
        public int RegisteredObjects { get; set; } = 0;
    }
}
