using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace User.Models
{
    public class User
    {
        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.CSharpLegacy)]
        public Guid Id { get; set; }
        [BsonElement("Name")]
        public string Name { get; set; } = string.Empty;
        public int RegisteredObjects { get; set; } = 0;

        [BsonElement("RegisteredObjectIds")]
        [JsonIgnore]
        public List<string> RegisteredObjectIds { get; set; } = new();
    }
}
