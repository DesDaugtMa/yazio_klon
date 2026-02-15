using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace TestApplication.Controllers
{
    public class ProductController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ProductController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // 1. Barcode Scan (wie bisher)
        [HttpPost]
        public async Task<IActionResult> GetProductInfo(string barcode)
        {
            if (string.IsNullOrEmpty(barcode)) return BadRequest("Kein Barcode gesendet.");

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("YazioKlonApp/1.0");

                var url = $"https://world.openfoodfacts.org/api/v2/product/{barcode}.json";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode) return NotFound("API Fehler.");

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("status", out var status) && status.GetInt32() != 1)
                {
                    return NotFound("Produkt nicht gefunden.");
                }

                return Json(ParseProduct(root.GetProperty("product")));
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Fehler: " + ex.Message);
            }
        }

        // 2. NEU: Text-Suche für Autocomplete
        [HttpGet]
        public async Task<IActionResult> SearchProducts(string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length < 3) return Json(new List<object>());

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("YazioKlonApp/1.0");

                // OpenFoodFacts Search API (Seite 1, max 5 Ergebnisse für Speed)
                var url = $"https://world.openfoodfacts.org/cgi/search.pl?search_terms={query}&search_simple=1&action=process&json=1&page_size=5";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return Json(new List<object>());

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var results = new List<object>();

                if (root.TryGetProperty("products", out var products) && products.ValueKind == JsonValueKind.Array)
                {
                    foreach (var product in products.EnumerateArray())
                    {
                        string name = product.TryGetProperty("product_name", out var n) ? n.GetString() : "Unbekannt";
                        string brand = product.TryGetProperty("brands", out var b) ? b.GetString() : "";
                        string id = product.TryGetProperty("_id", out var i) ? i.GetString() : ""; // Das ist der Barcode

                        if (!string.IsNullOrEmpty(id))
                        {
                            results.Add(new { Name = name, Brand = brand, Barcode = id });
                        }
                    }
                }

                return Json(results);
            }
            catch
            {
                return Json(new List<object>()); // Bei Fehler leere Liste
            }
        }

        // Hilfsfunktion zum Parsen (Code dedupliziert)
        private object ParseProduct(JsonElement product)
        {
            string name = product.TryGetProperty("product_name", out var n) ? n.GetString() : "Unbekanntes Produkt";
            string brand = product.TryGetProperty("brands", out var b) ? b.GetString() : "";
            var nutriments = product.GetProperty("nutriments");

            string GetNutrient(string key)
            {
                if (nutriments.TryGetProperty(key, out var el))
                {
                    if (el.ValueKind == JsonValueKind.Number) return el.GetDouble().ToString("0.0");
                    if (el.ValueKind == JsonValueKind.String) return el.GetString();
                }
                return "0";
            }

            return new
            {
                Name = name,
                Brand = brand,
                Calories = GetNutrient("energy-kcal_100g"),
                Protein = GetNutrient("proteins_100g"),
                Carbs = GetNutrient("carbohydrates_100g"),
                Fat = GetNutrient("fat_100g"),
                Unit = "100g"
            };
        }
    }
}