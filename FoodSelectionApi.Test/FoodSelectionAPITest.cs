using FoodSelection.Model;
using System.Net.Http.Json;
using Xunit;

namespace FoodSelectionApi.Test
{
    public class FoodSelectionAPITest
    {
        protected readonly HttpClient _client;
        private Random rand;
        private string[] Categories = { "Завтрак", "Обед", "Ужин" };
        private string[] NameProduct = { "Мясо", "Картошка", "Бургер", "Курица" };
        public FoodSelectionAPITest()
        {
            _client = new HttpClient();
            _client.BaseAddress = new Uri("https://localhost:7210/");
            rand = new Random();
        }
        [Fact]
        public async Task Add100Elements()
        {
            var ClRes = await _client.DeleteAsync("api/FoodProduct/DeleteAll");
            ClRes.EnsureSuccessStatusCode();

            int CountSuccess = 0;

            for(int i = 0; i < 100; i++)
            {
                var newElem = new FoodProduct
                {
                    Name = NameProduct[rand.Next(0,NameProduct.Length)], 
                    Category = Categories[rand.NextInt64(0, 2)],
                    Protein = rand.NextInt64(0, 100),
                    Carbs = rand.NextInt64(0, 100),
                    Fats = rand.NextInt64(0, 100),
                    Calories = 20,
                    IsVegan = false,
                    IsVegetarian = false
                };
                var resp = await _client.PostAsJsonAsync("api/FoodProduct", newElem);
                if (resp.IsSuccessStatusCode) CountSuccess++;
            }
            Assert.Equal(100, CountSuccess);
        }
        [Fact]
        public async Task Add10000Elements()
        {
            var ClRes = await _client.DeleteAsync("api/FoodProduct/DeleteAll");
            ClRes.EnsureSuccessStatusCode();

            int CountSuccess = 0;

            for (int i = 0; i < 10000; i++)
            {
                var newElem = new FoodProduct
                {
                    Name = NameProduct[rand.Next(0,3)],
                    Category = Categories[rand.Next(0, 2)],
                    Protein = rand.Next(0, 100),
                    Carbs = rand.Next(0, 100),
                    Fats = rand.Next(0, 100),
                    Calories = rand.Next(0, 1000),
                    IsVegan = false,
                    IsVegetarian = false
                };
                var resp = await _client.PostAsJsonAsync("api/FoodProduct", newElem);
                if (resp.IsSuccessStatusCode) CountSuccess++;
            }
            Assert.Equal(10000, CountSuccess);
        }

        [Fact]
        public async Task DeleteAll()
        {
            var getResponse = await _client.DeleteAsync("api/FoodProduct/DeleteAll");
            getResponse.EnsureSuccessStatusCode();
        }
    }
}
