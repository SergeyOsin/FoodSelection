using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FoodSelectionApi.Test;

public class FoodSelectionAPITest
{
    [DockerIntegrationFact]
    public async Task ConfirmationMessagesAreDeliveredAfterServicesResume()
    {
        var composeFile = FindComposeFile();
        var composeDirectory = Path.GetDirectoryName(composeFile)!;
        var foodUrl = Environment.GetEnvironmentVariable("FOOD_SERVICE_URL") ?? "http://localhost:7210/";
        var userUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL") ?? "http://localhost:7211/";
        using var foodClient = new HttpClient { BaseAddress = new Uri(foodUrl) };
        using var userClient = new HttpClient { BaseAddress = new Uri(userUrl) };
        var userStopped = false;
        var foodStopped = false;

        try
        {
            var userResponse = await userClient.PostAsJsonAsync("api/users", new
            {
                Name = $"Lab-{Guid.NewGuid():N}"
            });
            userResponse.EnsureSuccessStatusCode();
            using var userJson = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync());
            var userId = GetStringProperty(userJson.RootElement, "Id", "id");
            Assert.Equal(0, await GetRegisteredObjectsAsync(userClient, userId));

            await RunComposeAsync(composeDirectory, "stop", "user-service");
            userStopped = true;

            var productResponse = await foodClient.PostAsJsonAsync("api/FoodProduct", new
            {
                Name = "Async confirmation test",
                UserID = userId,
                Calories = 100,
                Protein = 10,
                Carbs = 20,
                Fats = 5,
                Category = "Test",
                IsVegan = false,
                IsVegetarian = false
            });
            productResponse.EnsureSuccessStatusCode();
            using var productJson = JsonDocument.Parse(await productResponse.Content.ReadAsStringAsync());
            var objectId = GetStringProperty(productJson.RootElement, "Id", "id");
            Assert.Equal("Pending", GetStringProperty(productJson.RootElement, "Status", "status"));
            await Task.Delay(TimeSpan.FromSeconds(6));

            await RunComposeAsync(composeDirectory, "stop", "foodservice1");
            foodStopped = true;

            await RunComposeAsync(composeDirectory, "start", "user-service");
            userStopped = false;
            await WaitForRegisteredObjectsAsync(userClient, userId, 1);
            Assert.Equal(1, await GetRegisteredObjectsAsync(userClient, userId));
            await Task.Delay(TimeSpan.FromSeconds(6));

            await RunComposeAsync(composeDirectory, "stop", "user-service");
            userStopped = true;
            await RunComposeAsync(composeDirectory, "start", "foodservice1");
            foodStopped = false;

            var confirmedProduct = await WaitForConfirmedProductAsync(foodClient, objectId);
            Assert.Equal("Confirmed", confirmedProduct.GetProperty("Status").GetString());
            Assert.True(confirmedProduct.GetProperty("ConfirmationTime").GetDateTime() > DateTime.UtcNow.AddMinutes(-2));
            await Task.Delay(TimeSpan.FromSeconds(6));
        }
        finally
        {
            if (foodStopped)
                await RunComposeAsync(composeDirectory, "start", "foodservice1");
            if (userStopped)
                await RunComposeAsync(composeDirectory, "start", "user-service");
        }
    }

    private static string GetStringProperty(JsonElement element, string firstName, string secondName) =>
        element.TryGetProperty(firstName, out var first)
            ? first.GetString()!
            : element.GetProperty(secondName).GetString()!;

    private static async Task<int> GetRegisteredObjectsAsync(HttpClient client, string userId)
    {
        var response = await client.GetAsync($"api/users/{userId}");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("registeredObjects").GetInt32();
    }

    private static async Task WaitForRegisteredObjectsAsync(HttpClient client, string userId, int minimum)
    {
        var timeout = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < timeout)
        {
            try
            {
                if (await GetRegisteredObjectsAsync(client, userId) >= minimum)
                    return;
            }
            catch (HttpRequestException)
            {
                // Compose start returns before ASP.NET is ready to accept requests.
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("UserService did not process the initial object-created event.");
    }

    private static async Task<JsonElement> WaitForConfirmedProductAsync(HttpClient client, string objectId)
    {
        var timeout = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < timeout)
        {
            try
            {
                var response = await client.GetAsync($"api/FoodProduct/{objectId}");
                if (response.IsSuccessStatusCode)
                {
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var product = json.RootElement.Clone();
                    if (product.TryGetProperty("Status", out var status) && status.GetString() == "Confirmed")
                        return product;
                }
            }
            catch (HttpRequestException)
            {
                // Compose start returns before ASP.NET is ready to accept requests.
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("FoodService did not persist the confirmation message.");
    }

    private static string FindComposeFile()
    {
        var configuredPath = Environment.GetEnvironmentVariable("DOCKER_COMPOSE_FILE");
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "docker-compose.yml");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException("Set DOCKER_COMPOSE_FILE or run the test from the repository.");
    }

    private static async Task RunComposeAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("compose");
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start Docker Compose.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"docker compose {string.Join(' ', arguments)} failed: {error}{output}");
    }
}

public sealed class DockerIntegrationFactAttribute : FactAttribute
{
    public DockerIntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_ASYNC_CONFIRMATION_INTEGRATION") != "1")
            Skip = "Set RUN_ASYNC_CONFIRMATION_INTEGRATION=1 to run the Docker/Kafka integration test.";
    }
}