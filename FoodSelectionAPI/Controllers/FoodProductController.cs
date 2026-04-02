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
    private readonly IFoodProductMetrics _foodProductService;

    public FoodProductController(GrafanService foodProductService) =>
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
        if (!ObjectId.TryParse(id, out var objectId))
            return BadRequest("Incorrent ID-format!");
        var product = await _foodProductService.GetByIdAsync(id);
        return product == null ? NotFound($"Product with ID {id} not found") : Ok(product);
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
        if (!ObjectId.TryParse(id, out var objectId))
            return BadRequest("Incorrent ID-format!");

        var result = await _foodProductService.UpdateAsync(id, updateDto);
        return result ? NoContent() : NotFound($"Product with ID {id} not found");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return BadRequest("Incorrect ID-format!");

        var result = await _foodProductService.DeleteAsync(id);
        return result ? Ok(new { message = "Продукт успешно удален" }) : NotFound($"Product with ID {id} not found");
    }

    [HttpDelete("DeleteAll")]
    public async Task<IActionResult> DeleteAll()
    {
        await _foodProductService.DeleteAllAsync();
        return Ok(new { message = "All products are DELETED!" });
    }
}
