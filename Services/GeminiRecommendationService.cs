using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recomandare_PC.Services;

public interface IGeminiRecommendationService
{
    Task<GeminiRecommendationResult> RecommendAsync(List<CartItem> cartItems);
}

public record GeminiRecommendationResult(
    List<GeminiRecommendedItem> Recommendations,
    string Explanation);

public record GeminiRecommendedItem(
    string Category,
    int ProductId,
    string ProductName,
    decimal Price,
    string Reason);

public class GeminiRecommendationService : IGeminiRecommendationService
{
    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiRecommendationService> _logger;
    private static readonly string[] AllCategories =
        ["CPU", "GPU", "RAM", "Motherboard", "PSU", "Storage", "Cooler", "Case"];

    public GeminiRecommendationService(
        IHttpClientFactory factory,
        AppDbContext db,
        IConfiguration configuration,
        ILogger<GeminiRecommendationService> logger)
    {
        _http    = factory.CreateClient("GeminiClient");
        _db      = db;
        _apiKey  = configuration["GeminiConfig:ApiKey"] ?? "";
        _model   = configuration["GeminiConfig:Model"] ?? "gemini-2.0-flash";
        _logger  = logger;
    }

    public async Task<GeminiRecommendationResult> RecommendAsync(List<CartItem> cartItems)
    {
        var cartCategories = cartItems
            .Select(c => c.CategoryName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = AllCategories.Where(c => !cartCategories.Contains(c)).ToList();
        if (missing.Count == 0)
            return new GeminiRecommendationResult([], "Configurația este completă — toate categoriile sunt acoperite.");

        var candidates = await _db.Products
            .Include(p => p.Category)
            .Where(p => missing.Contains(p.Category.Name) && p.StockQuantity > 0)
            .OrderBy(p => p.Price)
            .Take(60)
            .Select(p => new { p.Id, p.Name, p.Price, CategoryName = p.Category.Name })
            .ToListAsync();

        var cartSummary       = string.Join("\n", cartItems.Select(c => $"- {c.CategoryName}: {c.Name} ({c.Price:F2} RON)"));
        var candidatesSummary = string.Join("\n", candidates.Select(c => $"ID:{c.Id} [{c.CategoryName}] {c.Name} - {c.Price:F2} RON"));

        var prompt = $$"""
            Ești un expert în asamblarea calculatoarelor. Clientul are în coș:
            {{cartSummary}}

            Categorii lipsă: {{string.Join(", ", missing)}}

            Produse disponibile în stoc (alege DOAR din această listă):
            {{candidatesSummary}}

            Recomandă câte un produs pentru fiecare categorie lipsă.
            Returnează STRICT JSON cu schema:
            {
              "recommendations": [
                { "category": "string", "productId": number, "productName": "string", "price": number, "reason": "string (max 80 chars)" }
              ],
              "explanation": "string (max 200 chars)"
            }
            """;

        var requestBody = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.2
            }
        };

        var url  = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
        var body = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        try
        {
            var response     = await _http.PostAsync(url, body);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API {Status}: {Body}", response.StatusCode, responseText);
                return new GeminiRecommendationResult([], $"Eroare API Gemini ({response.StatusCode}).");
            }

            using var doc  = JsonDocument.Parse(responseText);
            var jsonText   = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "{}";

            var opts   = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<GeminiJsonResult>(jsonText, opts);

            if (result is null)
                return new GeminiRecommendationResult([], "Răspuns invalid de la Gemini.");

            var items = result.Recommendations?
                .Select(r => new GeminiRecommendedItem(
                    r.Category    ?? "",
                    r.ProductId,
                    r.ProductName ?? "",
                    r.Price,
                    r.Reason      ?? ""))
                .ToList() ?? [];

            return new GeminiRecommendationResult(items, result.Explanation ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeminiRecommendationService failed");
            return new GeminiRecommendationResult([], "Eroare internă la generarea recomandărilor.");
        }
    }

    // ── Private deserialization types ────────────────────────────────────────────
    private record GeminiJsonResult(
        [property: JsonPropertyName("recommendations")] List<GeminiJsonItem>? Recommendations,
        [property: JsonPropertyName("explanation")]     string?               Explanation);

    private record GeminiJsonItem(
        [property: JsonPropertyName("category")]    string?  Category,
        [property: JsonPropertyName("productId")]   int      ProductId,
        [property: JsonPropertyName("productName")] string?  ProductName,
        [property: JsonPropertyName("price")]       decimal  Price,
        [property: JsonPropertyName("reason")]      string?  Reason);
}
