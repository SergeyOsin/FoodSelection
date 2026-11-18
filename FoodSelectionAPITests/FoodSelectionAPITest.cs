using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using Xunit;

namespace FoodSelectionApi.Test;

public class FoodSelectionAPITest
{
    [DockerIntegrationFact]
    public async Task ConfirmationMessagesAreDeliveredAfterServicesResume()
    {
        var composeFile = FindComposeFile();
        var composeDirectory = Path.GetDirectoryName(composeFile)!;
        var apiUrl = Environment.GetEnvironmentVariable("API_GATEWAY_URL") ?? "http://localhost:7210/";
        using var foodClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        using var userClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        var userPaused = false;
        var foodPaused = false;

        try
        {
            var anonymousResponse = await foodClient.GetAsync("api/FoodProduct");
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

            var username = $"async-{Guid.NewGuid():N}";
            var userResponse = await userClient.PostAsJsonAsync("api/auth/register", new
            {
                Username = username,
                Name = "Async confirmation test",
                Password = "Test-password-123!"
            });
            userResponse.EnsureSuccessStatusCode();
            using var userJson = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync());
            var userId = userJson.RootElement.GetProperty("userId").GetString()!;
            var loginResponse = await userClient.PostAsJsonAsync("api/auth/login", new
            {
                Username = username,
                Password = "Test-password-123!"
            });
            loginResponse.EnsureSuccessStatusCode();
            using var loginJson = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
            var accessToken = loginJson.RootElement.GetProperty("accessToken").GetString()!;
            foodClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            userClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

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
            var objectId = productJson.RootElement.TryGetProperty("Id", out var productId)
                ? productId.GetString()!
                : productJson.RootElement.GetProperty("id").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(objectId));

            var initialRegisteredObjects = await GetRegisteredObjectsAsync(userClient, userId);
            await WaitForRegisteredObjectsAsync(userClient, userId, initialRegisteredObjects + 1);
            initialRegisteredObjects = await GetRegisteredObjectsAsync(userClient, userId);

            await RunComposeAsync(composeDirectory, "pause", "user-service");
            userPaused = true;
            await RunComposeAsync(composeDirectory, "pause", "foodservice1");
            foodPaused = true;

            using var confirmationObserver = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
            {
                BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092",
                GroupId = $"confirmation-test-{Guid.NewGuid():N}",
                AutoOffsetReset = AutoOffsetReset.Latest,
                EnableAutoCommit = false
            }).Build();
            confirmationObserver.Subscribe("confirmation-topic");
            confirmationObserver.Consume(TimeSpan.FromMilliseconds(250));

            using var producer = new ProducerBuilder<Null, string>(new ProducerConfig
            {
                BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092"
            }).Build();
            await producer.ProduceAsync("object-created-topic", new Message<Null, string>
            {
                Value = JsonSerializer.Serialize(new { Id = objectId, UserID = userId })
            });

            await RunComposeAsync(composeDirectory, "unpause", "user-service");
            userPaused = false;

            var confirmation = WaitForConfirmation(confirmationObserver, objectId);
            Assert.Equal(objectId, confirmation.ObjectId);
            Assert.True(confirmation.ConfirmationTime > DateTimeOffset.UtcNow.AddMinutes(-2));
            Assert.Equal(initialRegisteredObjects + 1, await GetRegisteredObjectsAsync(userClient, userId));

            await RunComposeAsync(composeDirectory, "pause", "user-service");
            userPaused = true;
            await RunComposeAsync(composeDirectory, "unpause", "foodservice1");
            foodPaused = false;

            var confirmedProduct = await WaitForConfirmedProductAsync(foodClient, objectId);
            Assert.Equal("Confirmed", confirmedProduct.GetProperty("Status").GetString());
            Assert.True(confirmedProduct.GetProperty("ConfirmationTime").GetDateTime() > DateTime.UtcNow.AddMinutes(-2));
        }
        finally
        {
            if (foodPaused)
                await RunComposeAsync(composeDirectory, "unpause", "foodservice1");
            if (userPaused)
                await RunComposeAsync(composeDirectory, "unpause", "user-service");
        }
    }

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
            if (await GetRegisteredObjectsAsync(client, userId) >= minimum)
                return;
            await Task.Delay(250);
        }

        throw new TimeoutException("UserService did not process the initial object-created event.");
    }

    private static ConfirmationMessage WaitForConfirmation(
        IConsumer<Ignore, string> consumer,
        string objectId)
    {
        var timeout = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < timeout)
        {
            var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
            if (result is null)
                continue;

            var confirmation = JsonSerializer.Deserialize<ConfirmationMessage>(
                result.Message.Value,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (confirmation?.ObjectId == objectId)
                return confirmation;
        }

        throw new TimeoutException("UserService did not publish the confirmation message.");
    }

    private static async Task<JsonElement> WaitForConfirmedProductAsync(HttpClient client, string objectId)
    {
        var timeout = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < timeout)
        {
            var response = await client.GetAsync($"api/FoodProduct/{objectId}");
            if (response.IsSuccessStatusCode)
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var product = json.RootElement.Clone();
                if (product.TryGetProperty("Status", out var status) && status.GetString() == "Confirmed")
                    return product;
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

    private sealed class ConfirmationMessage
    {
        public string ObjectId { get; set; } = string.Empty;
        public DateTimeOffset ConfirmationTime { get; set; }
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