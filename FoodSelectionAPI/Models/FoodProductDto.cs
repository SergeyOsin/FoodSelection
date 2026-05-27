using MongoDB.Bson.Serialization.Attributes;

namespace FoodSelection.Models
{
    public class FoodProductDto
    {
        public string Name { get; set; } = string.Empty;
        public string UserID { get; set; }

        public string Status { get; set; } = "Pending";
        public int Calories { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fats { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsVegan { get; set; }
        public bool IsVegetarian { get; set; }
    }

    
}
