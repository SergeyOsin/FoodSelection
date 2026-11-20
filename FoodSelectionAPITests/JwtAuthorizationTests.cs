using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FoodSelectionApi.Test;

public class JwtAuthorizationTests
{
    private static readonly ProtectedEndpoint[] ProtectedEndpoints =
    [
        new("UserService", "api/users", Environment.GetEnvironmentVariable("USER_SERVICE_URL") ?? "http://localhost:7211/"),
        new("FoodService", "api/FoodProduct", Environment.GetEnvironmentVariable("FOOD_SERVICE_URL") ?? "http://localhost:7212/")
    ];

    [DockerIntegrationFact]
    public async Task ProtectedEndpointsRejectMissingFakeAndExpiredTokens()
    {
        var expiredToken = CreateExpiredToken();

        foreach (var endpoint in ProtectedEndpoints)
        {
            using var client = new HttpClient { BaseAddress = new Uri(endpoint.BaseUrl) };

            using var anonymousResponse = await client.GetAsync(endpoint.Path);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                "not-a-valid-jwt");
            using var fakeTokenResponse = await client.GetAsync(endpoint.Path);
            Assert.Equal(HttpStatusCode.Unauthorized, fakeTokenResponse.StatusCode);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
            using var expiredTokenResponse = await client.GetAsync(endpoint.Path);
            Assert.Equal(HttpStatusCode.Unauthorized, expiredTokenResponse.StatusCode);
        }
    }

    [DockerIntegrationFact]
    public async Task EndpointsAcceptTokenIssuedByAuthenticationService()
    {
        var gatewayUrl = Environment.GetEnvironmentVariable("API_GATEWAY_URL") ?? "http://localhost:7210/";
        using var authClient = new HttpClient { BaseAddress = new Uri(gatewayUrl) };
        var username = $"jwt-test-{Guid.NewGuid():N}";
        const string password = "Jwt-test-password-123!";

        using var registerResponse = await authClient.PostAsJsonAsync("api/auth/register", new
        {
            Username = username,
            Name = "JWT authorization test",
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        using var loginResponse = await authClient.PostAsJsonAsync("api/auth/login", new
        {
            Username = username,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginJson = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var accessToken = loginJson.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        foreach (var endpoint in ProtectedEndpoints)
        {
            using var client = new HttpClient { BaseAddress = new Uri(endpoint.BaseUrl) };
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.GetAsync(endpoint.Path);
            Assert.True(
                response.IsSuccessStatusCode,
                $"{endpoint.Name} rejected a token issued by AuthService: {(int)response.StatusCode}.");
        }
    }

    private static string CreateExpiredToken()
    {
        var signingKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException(
                "Set JWT_SIGNING_KEY to the same key used by AuthService and both microservices.");

        var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "FoodSelection";
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "FoodSelectionClients";
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            notBefore: now.AddHours(-2),
            expires: now.AddHours(-1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record ProtectedEndpoint(string Name, string Path, string BaseUrl);
}
