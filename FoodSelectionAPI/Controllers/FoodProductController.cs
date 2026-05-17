using FoodSelection.Model;
using FoodSelection.Models;
using FoodSelection.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Collections.Generic;

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

    [HttpPost]
    public async Task<ActionResult<FoodProductResponseDto>> Create([FromBody] FoodProductCreateDto createDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        var result = await _foodProductService.CreateAsync(createDto);
        return CreatedAtAction(nameof(GetAllRedisKeys), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] FoodProductCreateDto updateDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        if (!ObjectId.TryParse(id, out var objectId))
            return BadRequest("Incorrect ID-format!");

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

    [HttpGet("redis/AllKeys")]
    public async Task<ActionResult<IEnumerable<string>>> GetAllRedisKeys()
    {
        var keys = await _foodProductService.GetAllAsync();
        return Ok(keys);
    }

    [HttpDelete("redis/Delete")]
    public async Task<IActionResult> DeleteAllKeys()
    {
        await _foodProductService.DeleteAllAsync();
        return Ok(new { message = "Redis cache deleted." });
    }

    [HttpGet("redis/key/{key}")]
    public async Task<IActionResult> GetRedisKey(string key)
    {
        if (!ObjectId.TryParse(key, out var objectId))
            return BadRequest("Incorrect ID-format!");
        await _foodProductService.GetByIdAsync(key);
        return Ok(new { message = $"Key '{key}' deleted." });
    }
}