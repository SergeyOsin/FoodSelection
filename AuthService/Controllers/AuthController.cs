using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Models;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthUserStore _users;
    private readonly IConfiguration _configuration;

    public AuthController(AuthUserStore users, IConfiguration configuration)
    {
        _users = users;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<ActionResult<TokenResponse>> Register(RegisterRequest request)
    {
        var user = await _users.RegisterAsync(request);
        if (user is null)
            return Conflict(new { error = "Username is already registered." });

        return Ok(CreateToken(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request)
    {
        var user = await _users.FindByUsernameAsync(request.Username);
        if (user is null || !AuthUserStore.VerifyPassword(request.Password, user))
            return Unauthorized(new { error = "Invalid username or password." });

        return Ok(CreateToken(user));
    }

    private TokenResponse CreateToken(AuthUser user)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key must be configured.");
        var issuer = _configuration["Jwt:Issuer"] ?? "FoodSelection";
        var audience = _configuration["Jwt:Audience"] ?? "FoodSelectionClients";
        var expires = DateTime.UtcNow.AddHours(1);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        };
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        return new TokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds,
            UserId = user.Id,
            Name = user.Name
        };
    }
}
