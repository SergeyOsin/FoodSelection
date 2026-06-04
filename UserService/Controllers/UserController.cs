using Microsoft.AspNetCore.Mvc;
using User.Models;
using UserService.Models;

namespace User.Controllers;

using User = User.Models.User;
[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly ServiceUser _service;

    public UserController(ServiceUser service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<User>>> GetAll()
    {
        var users = await _service.GetAllAsync();
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<User>> GetById(Guid id)
    {
        var user = await _service.GetByIdAsync(id);

        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<User>> Create([FromBody] CreateDTOUser userDto)
    {
        var createdUserId = await _service.CreateAsync(userDto);

        var createdUser = await _service.GetByIdAsync(createdUserId);

        return CreatedAtAction(
            nameof(GetById),
            new { id = createdUserId },
            createdUser);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] User newUser)
    {
        var existingUser = await _service.GetByIdAsync(id);

        if (existingUser == null)
            return NotFound();

        await _service.UpdateAsync(id, newUser);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existingUser = await _service.GetByIdAsync(id);

        if (existingUser == null)
            return NotFound();

        await _service.DeleteAsync(id);

        return NoContent();
    }
}