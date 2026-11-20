using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FoodSelection.Model;

[BsonIgnoreExtraElements]
public class FoodProduct
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid UserID { get; set; }

    [BsonElement("UserName")]
    public string UserName { get; set; } = string.Empty;

    [BsonElement("Status")]
    public string Status { get; set; } = "Pending";

    [BsonElement("Name")]
    public string Name { get; set; } = null!;

    [BsonElement("Calories")]
    public int Calories { get; set; }

    [BsonElement("Protein")]
    public double Protein { get; set; }

    [BsonElement("Carbs")]
    public double Carbs { get; set; }

    [BsonElement("Fats")]
    public double Fats { get; set; }

    [BsonElement("Category")]
    public string Category { get; set; } = null!;

    [BsonElement("IsVegan")]
    public bool IsVegan { get; set; }

    [BsonElement("IsVegetarian")]
    public bool IsVegetarian { get; set; }

    [BsonElement("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("ConfirmationTime")]
    public DateTime? ConfirmationTime { get; set; }
}