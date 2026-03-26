using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FoodSelection.Model;

public class FoodProduct
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set;}
    public string Name { get; set; } = string.Empty;
    public int Calories { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fats { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsVegan { get; set; }
    public bool IsVegetarian { get; set; }
}