//using FoodSelection.Model;
//using System.Net.Http.Json;
//using Xunit;

//namespace FoodSelectionApi.Test
//{
//    public class FoodSelectionAPITest
//    {
//        protected readonly HttpClient _client;
//        private readonly Random rand = new();
//        private readonly string[] Categories = { "Завтрак", "Обед", "Ужин" };
//        private readonly string[] NameProduct = { "Мясо", "Картошка", "Бургер", "Курица" };

//        public FoodSelectionAPITest()
//        {
//            var apiUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "https://localhost:7210/";
//            _client = new HttpClient { BaseAddress = new Uri(apiUrl) };
//        }

//        [Fact]
//        public async Task DeleteAll()
//        {
//            var getResponse = await _client.DeleteAsync("api/FoodProduct/DeleteAll");
//            getResponse.EnsureSuccessStatusCode();
//        }

//        [Fact]
//        public async Task Add100Elements()
//        {
//            await _client.DeleteAsync("api/FoodProduct/DeleteAll");

//            int countSuccess = 0;

//            for (int i = 0; i < 100; i++)
//            {
//                var newElem = new FoodProduct
//                {
//                    Name = NameProduct[rand.Next(0, NameProduct.Length)],
//                    Category = Categories[rand.Next(0, Categories.Length)],
//                    Protein = rand.Next(0, 100),
//                    Carbs = rand.Next(0, 100),
//                    Fats = rand.Next(0, 100),
//                    Calories = rand.Next(0, 1000),
//                    IsVegan = false,
//                    IsVegetarian = false
//                };

//                var resp = await _client.PostAsJsonAsync("api/FoodProduct", newElem);
//                if (resp.IsSuccessStatusCode)
//                    countSuccess++;
//            }

//            Assert.Equal(100, countSuccess);
//        }

//        [Fact]
//        public async Task GetAll()
//        {
//            var response = await _client.GetAsync("api/FoodProduct");
//            response.EnsureSuccessStatusCode();
//        }
//    }
//}