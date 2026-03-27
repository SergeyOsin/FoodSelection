using FoodSelection.Models;
using FoodSelection.Model;

namespace FoodSelection.Services
{
    public interface IFoodProductMetrics
    {
        Task<List<FoodProductResponseDto>> GetAllAsync();
        Task<List<FoodProductResponseDto>> FilterAsync(FoodProductFilterDto filter);
        Task<FoodProductResponseDto?> GetByIdAsync(string id);
        Task<FoodProductResponseDto> CreateAsync(FoodProductCreateDto createDto);
        Task<bool> UpdateAsync(string id, FoodProductCreateDto updateDto);
        Task<bool> DeleteAsync(string id);
        Task DeleteAllAsync();
    }
}
