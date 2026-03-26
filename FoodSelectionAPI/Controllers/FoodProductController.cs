using FoodSelection.Model;
using FoodSelection.Models;
using FoodSelection.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace FoodSelection.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoodProductController : ControllerBase
{
    private readonly IFoodProductService _foodProductService;

    public FoodProductController(FoodProductService foodProductService) =>
        _foodProductService = foodProductService;

    [HttpGet]
    public async Task<ActionResult<List<FoodProductResponseDto>>> GetAll() =>
        Ok(await _foodProductService.GetAllAsync());

    [HttpGet("filter")]
    public async Task<ActionResult<List<FoodProductResponseDto>>> Filter(
        [FromQuery] FoodProductFilterDto filter)
    {         
        var products = await _foodProductService.FilterAsync(filter);
        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FoodProductResponseDto>> GetById(string id)
    {
        var product = await _foodProductService.GetByIdAsync(id);
        return product == null ? NotFound($"Продукт с ID {id} не найден") : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<FoodProductResponseDto>> Create([FromBody] FoodProductCreateDto createDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _foodProductService.CreateAsync(createDto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] FoodProductCreateDto updateDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _foodProductService.UpdateAsync(id, updateDto);
        return result ? NoContent() : NotFound($"Продукт с ID {id} не найден");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {

        var result = await _foodProductService.DeleteAsync(id);
        return result ? Ok(new { message = "Продукт успешно удален" }) : NotFound($"Продукт с ID {id} не найден");
    }

    [HttpDelete("DeleteAll")]
    public async Task<IActionResult> DeleteAll()
    {
        await _foodProductService.DeleteAllAsync();
        return Ok(new { message = "Удалены все продукты" });
    }
}
