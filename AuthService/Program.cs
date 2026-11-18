using System.Text;
using AuthService.Models;
using AuthService.Services;

var builder = WebApplication.CreateBuilder(args);

var signingKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
    throw new InvalidOperationException("Jwt:Key must be configured with at least 32 bytes.");

builder.Services.Configure<AuthDatabaseSettings>(builder.Configuration.GetSection("Database"));
builder.Services.AddSingleton<AuthUserStore>();
builder.Services.AddControllers();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();
app.Run();
