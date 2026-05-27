using FoodSelection.Model;
using FoodSelection.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FoodSelectionApi.Test
{
    public class FoodSelectionCacheTest
    {
        protected readonly HttpClient _client;
        private readonly Random rand = new();

        public FoodSelectionCacheTest()
        {
            var apiUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "https://localhost:7210/";
            _client = new HttpClient { BaseAddress = new Uri(apiUrl) };
        }

        [Fact]
        public async Task GetAll_Cache()
        {
            await _client.DeleteAsync("api/FoodProduct/DeleteAll");

            var newElem = new FoodProduct
            {
                Name = "Тест скорости",
                Category = "Завтрак",
                Protein = 10,
                Carbs = 20,
                Fats = 5,
                Calories = 100,
                IsVegan = false,
                IsVegetarian = false
            };

            await _client.PostAsJsonAsync("api/FoodProduct", newElem);

            var sw1 = Stopwatch.StartNew();
            var response1 = await _client.GetAsync("api/FoodProduct");
            sw1.Stop();

            var sw2 = Stopwatch.StartNew();
            var response2 = await _client.GetAsync("api/FoodProduct");
            sw2.Stop();

            Assert.True(response1.IsSuccessStatusCode);
            Assert.True(response2.IsSuccessStatusCode);
            Assert.True(sw2.ElapsedMilliseconds <= sw1.ElapsedMilliseconds + 50,
                $"Первый запрос: {sw1.ElapsedMilliseconds}ms, Второй (кэш): {sw2.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task Add100Elements_Cache()
        {
            await _client.DeleteAsync("api/FoodProduct/DeleteAll");

            string[] Categories = { "Завтрак", "Обед", "Ужин" };
            string[] NameProduct = { "Мясо", "Картошка", "Бургер", "Курица" };
            var rand = new Random();

            int countSuccess = 0;

            for (int i = 0; i < 100; i++)
            {
                var newElem = new FoodProduct
                {
                    Name = NameProduct[rand.Next(0, NameProduct.Length)],
                    Category = Categories[rand.Next(0, Categories.Length)],
                    Protein = rand.Next(0, 100),
                    Carbs = rand.Next(0, 100),
                    Fats = rand.Next(0, 100),
                    Calories = rand.Next(0, 1000),
                    IsVegan = false,
                    IsVegetarian = false
                };

                var resp = await _client.PostAsJsonAsync("api/FoodProduct", newElem);
                if (resp.IsSuccessStatusCode)
                {
                    countSuccess++;
                }
            }

            Assert.Equal(100, countSuccess);

            var response1 = await _client.GetAsync("api/FoodProduct");
            response1.EnsureSuccessStatusCode();

            var content1 = await response1.Content.ReadFromJsonAsync<List<FoodProductResponseDto>>();
            Assert.Equal(1000, content1?.Count);
        }

        [Fact]
        public async Task DeleteProduct_Cache()
        {
            await _client.DeleteAsync("api/FoodProduct/DeleteAll");

            var newElem = new FoodProduct
            {
                Name = "Продукт на удаление",
                Category = "Ужин",
                Protein = 10,
                Carbs = 20,
                Fats = 5,
                Calories = 100,
                IsVegan = false,
                IsVegetarian = false
            };

            var postRes = await _client.PostAsJsonAsync("api/FoodProduct", newElem);
            var createdItem = await postRes.Content.ReadFromJsonAsync<FoodProductResponseDto>();
            string id = createdItem!.Id;

            await _client.GetAsync($"api/FoodProduct/{id}");

            var deleteRes = await _client.DeleteAsync($"api/FoodProduct/{id}");
            deleteRes.EnsureSuccessStatusCode();

            var getRes = await _client.GetAsync($"api/FoodProduct/{id}");

            Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
        }
    }
}