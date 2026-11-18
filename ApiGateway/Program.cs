using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var foodServiceBaseUrl = builder.Configuration["Services:FoodService"]
    ?? throw new InvalidOperationException("Services:FoodService must be configured.");
var userServiceBaseUrl = builder.Configuration["Services:UserService"]
    ?? throw new InvalidOperationException("Services:UserService must be configured.");
var authServiceBaseUrl = builder.Configuration["Services:AuthService"]
    ?? throw new InvalidOperationException("Services:AuthService must be configured.");
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key must be configured with at least 32 bytes.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FoodSelection";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "FoodSelectionClients";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpClient("gateway", client =>
{
    client.Timeout = TimeSpan.FromSeconds(100);
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.UseAuthentication();
app.UseAuthorization();

app.Map("/api/auth/{**path}", ProxyAsync).AllowAnonymous();
app.Map("/{**path}", ProxyAsync).RequireAuthorization();

app.Run();

async Task ProxyAsync(HttpContext context, IHttpClientFactory clientFactory, ILogger<Program> logger)
{
    var path = context.Request.Path;
    string serviceBaseUrl;

    if (path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase))
        serviceBaseUrl = authServiceBaseUrl;
    else if (path.StartsWithSegments("/api/users", StringComparison.OrdinalIgnoreCase))
        serviceBaseUrl = userServiceBaseUrl;
    else if (path.StartsWithSegments("/api/FoodProduct", StringComparison.OrdinalIgnoreCase))
        serviceBaseUrl = foodServiceBaseUrl;
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await context.Response.WriteAsJsonAsync(new { error = "No API route matches this path." });
        return;
    }

    var targetUri = new Uri(
        new Uri(serviceBaseUrl, UriKind.Absolute),
        $"{path.Value?.TrimStart('/')}{context.Request.QueryString}");
    using var outgoingRequest = new HttpRequestMessage(
        new HttpMethod(context.Request.Method),
        targetUri);

    if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        outgoingRequest.Content = new StreamContent(context.Request.Body);

    foreach (var header in context.Request.Headers)
    {
        if (IsHopByHopHeader(header.Key) || header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
            continue;

        if (!outgoingRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            outgoingRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    try
    {
        var client = clientFactory.CreateClient("gateway");
        using var upstreamResponse = await client.SendAsync(
            outgoingRequest,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);

        context.Response.StatusCode = (int)upstreamResponse.StatusCode;
        foreach (var header in upstreamResponse.Headers)
        {
            if (!IsHopByHopHeader(header.Key))
                context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in upstreamResponse.Content.Headers)
        {
            if (!IsHopByHopHeader(header.Key))
                context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        context.Response.Headers.Remove("transfer-encoding");
        await upstreamResponse.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
    }
    catch (HttpRequestException exception)
    {
        logger.LogError(exception, "API Gateway could not reach upstream service at {TargetUri}", targetUri);
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(
                new { error = "The requested service is currently unavailable." },
                context.RequestAborted);
        }
    }
}

static bool IsHopByHopHeader(string headerName) =>
    headerName.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
    headerName.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
    headerName.Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase) ||
    headerName.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
    headerName.Equals("TE", StringComparison.OrdinalIgnoreCase) ||
    headerName.Equals("Trailer", StringComparison.OrdinalIgnoreCase) ||
    headerName.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
    headerName.Equals("Upgrade", StringComparison.OrdinalIgnoreCase);
