using Microsoft.AspNetCore.Mvc;
using User.Models;

namespace User.Controllers;

using User.Models;
[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly ServiceUser _service;

    public UserController(ServiceUser service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<User>>> GetAll()=>  Ok(await _service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetById(string id)
    {
        var user = await _service.GetByIdAsync(id);

        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<User>> Create(User user)
    {
        var NewUser = new User
        {
            Name = "Sergey",
            RegisteredObjects=0,
   
        };

       await _service.CreateAsync(NewUser);
       return Ok(NewUser);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, User newUser)
    {
        var user = await _service.GetByIdAsync(id);

        if (user == null)
            return NotFound();

        await _service.UpdateAsync(id, newUser);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _service.GetByIdAsync(id);
        if (user == null) return NotFound();
        await _service.DeleteAsync(id);
        return NoContent();
    }
}