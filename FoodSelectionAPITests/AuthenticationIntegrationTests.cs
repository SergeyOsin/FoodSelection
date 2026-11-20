using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FoodSelectionApi.Test;

public class AuthenticationIntegrationTests
{
    private static readonly string[] ProtectedEndpoints =
    [
        "api/FoodProduct",
        "api/users"
    ];

    [AuthenticationIntegrationFact]
    public async Task GatewayRootIsAvailableAndProtectedApiRequiresBearerToken()
    {
        var apiUrl = Environment.GetEnvironmentVariable("API_GATEWAY_URL") ?? "http://localhost:7210/";
        using var client = new HttpClient { BaseAddress = new Uri(apiUrl) };

        using var rootResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, rootResponse.StatusCode);

        using var protectedResponse = await client.GetAsync("api/FoodProduct");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }

    [AuthenticationIntegrationFact]
    public async Task ProtectedEndpointsRejectAnonymousFakeAndExpiredTokens()
    {
        using var client = CreateClient();

        foreach (var endpoint in ProtectedEndpoints)
        {
            using var anonymousResponse = await SendGetAsync(client, endpoint);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

            using var fakeTokenResponse = await SendGetAsync(client, endpoint, "fake.token.value");
            Assert.Equal(HttpStatusCode.Unauthorized, fakeTokenResponse.StatusCode);

            using var expiredTokenResponse = await SendGetAsync(client, endpoint, CreateExpiredToken());
            Assert.Equal(HttpStatusCode.Unauthorized, expiredTokenResponse.StatusCode);
        }
    }

    [AuthenticationIntegrationFact]
    public async Task LoginTokenAllowsAccessToProtectedEndpoints()
    {
        using var client = CreateClient();
        var user = await RegisterAndLoginAsync(client);

        using var userResponse = await SendGetAsync(client, $"api/users/{user.UserId}", user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);

        using var foodResponse = await SendGetAsync(client, "api/FoodProduct", user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, foodResponse.StatusCode);
    }

    [AuthenticationIntegrationFact]
    public async Task LogoutRevokesPreviouslyIssuedToken()
    {
        using var client = CreateClient();
        var user = await RegisterAndLoginAsync(client);

        using var initialResponse = await SendGetAsync(client, "api/FoodProduct", user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        using var logoutResponse = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var responseAfterLogout = await SendGetAsync(client, "api/FoodProduct", user.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, responseAfterLogout.StatusCode);

    }

    private static HttpClient CreateClient()
    {
        var apiUrl = Environment.GetEnvironmentVariable("API_GATEWAY_URL") ?? "http://localhost:7210/";
        return new HttpClient { BaseAddress = new Uri(apiUrl) };
    }

    private static async Task<(string UserId, string AccessToken)> RegisterAndLoginAsync(HttpClient client)
    {
        var username = $"auth-test-{Guid.NewGuid():N}";
        const string password = "Test-password-123!";

        using var registrationResponse = await client.PostAsJsonAsync("api/auth/register", new
        {
            Username = username,
            Name = "Authentication integration test",
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, registrationResponse.StatusCode);

        using var rejectedLoginResponse = await client.PostAsJsonAsync("api/auth/login", new
        {
            Username = username,
            Password = password + "-incorrect"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedLoginResponse.StatusCode);

        using var loginResponse = await client.PostAsJsonAsync("api/auth/login", new
        {
            Username = username,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginJson = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var root = loginJson.RootElement;
        var accessToken = root.GetProperty("accessToken").GetString();
        var userId = root.GetProperty("userId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(userId));

        return (userId!, accessToken!);
    }

    private static async Task<HttpResponseMessage> SendGetAsync(
        HttpClient client,
        string endpoint,
        string? accessToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await client.SendAsync(request);
    }

    private static string CreateExpiredToken()
    {
        var signingKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException(
                "Set JWT_SIGNING_KEY to the same secret used by Docker Compose (at least 32 bytes).");

        var now = DateTimeOffset.UtcNow;
        var header = Base64UrlEncode("""{"alg":"HS256","typ":"JWT"}""");
        var payload = Base64UrlEncode(JsonSerializer.Serialize(new
        {
            iss = "FoodSelection",
            aud = "FoodSelectionClients",
            sub = Guid.NewGuid().ToString(),
            jti = Guid.NewGuid().ToString("N"),
            nbf = now.AddHours(-1).ToUnixTimeSeconds(),
            exp = now.AddMinutes(-5).ToUnixTimeSeconds()
        }));
        var unsignedToken = $"{header}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var signature = Base64UrlEncode(hmac.ComputeHash(Encoding.ASCII.GetBytes(unsignedToken)));
        return $"{unsignedToken}.{signature}";
    }

    private static string Base64UrlEncode(string value) =>
        Base64UrlEncode(Encoding.UTF8.GetBytes(value));

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed class AuthenticationIntegrationFactAttribute : FactAttribute
{
    public AuthenticationIntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_AUTHENTICATION_INTEGRATION") != "1")
            Skip = "Set RUN_AUTHENTICATION_INTEGRATION=1 to run the Docker-backed authentication tests.";
    }
}
