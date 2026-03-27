namespace FoodSelection.Models
{
    public class FoodProductFilterDto
    {
        public string? Category {  get; set; }
        public bool? IsVegan { get; set; }
        public bool? IsVegetarian { get; set; }
        public int? MinCalories { get; set; }
        public int? MaxCalories { get; set; }
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }

    }
}
