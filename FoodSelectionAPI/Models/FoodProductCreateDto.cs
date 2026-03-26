using System.ComponentModel.DataAnnotations;

namespace FoodSelection.Models
{
    public class FoodProductCreateDto
    {

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(0, 1000)]
        public int Calories { get; set; }

        [Range(0, 100)]
        public double Protein { get; set; }

        [Range(0, 100)]
        public double Carbs { get; set; }

        [Range(0, 100)]
        public double Fats { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty;

        public bool IsVegan { get; set; }
        public bool IsVegetarian { get; set; }
    }
}
