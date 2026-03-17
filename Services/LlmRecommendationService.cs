using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.DTOs;
using Recomandare_PC.Models;

namespace Recomandare_PC.Services;

public interface ILlmRecommendationService
{
    Task<RecommendationResponse> RecommendAsync(RecommendationRequest request);
}

public class LlmRecommendationService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<LlmRecommendationService> logger) : ILlmRecommendationService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<RecommendationResponse> RecommendAsync(RecommendationRequest request)
    {
        // 1. Identify which PC categories are missing
        var allCategories = await db.Categories.Select(c => c.Name).ToListAsync();
        var existingCategoryNames = request.ExistingComponents
            .Select(e => e.Category.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingCategories = allCategories
            .Where(c => !existingCategoryNames.Contains(c))
            .ToList();

        // 2. Fetch in-stock candidates for the missing categories
        var candidates = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .Where(p => p.StockQuantity > 0 &&
                        missingCategories.Contains(p.Category.Name))
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.Price)
            .Take(60) // keep context reasonable
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Price,
                p.StockQuantity,
                Category = p.Category.Name,
                Subcategory = p.Subcategory == null ? null : p.Subcategory.Name,
                p.Specifications
            })
            .ToListAsync();

        if (candidates.Count == 0)
        {
            return new RecommendationResponse([], "No in-stock products found for the missing categories.");
        }

        // 3. Build the prompt
        var prompt = BuildPrompt(request, missingCategories, candidates
            .Select(c => new CandidateInfo(c.Id, c.Name, c.Price, c.Category, c.Subcategory, c.Specifications))
            .ToList());

        // 4. Call the LLM
        var llmRaw = await CallLlmAsync(prompt);

        // 5. Deserialize the strict JSON response
        return DeserializeLlmResponse(llmRaw);
    }

    // -------------------------------------------------------------------------
    // Prompt builder
    // -------------------------------------------------------------------------

    private static string BuildPrompt(
        RecommendationRequest request,
        List<string> missingCategories,
        List<CandidateInfo> candidates)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a PC hardware expert. Your task is to recommend the best compatible components.");
        sb.AppendLine();

        sb.AppendLine("## User's current components:");
        if (request.ExistingComponents.Count > 0)
            foreach (var c in request.ExistingComponents)
                sb.AppendLine($"- {c.Category}: {c.Name}");
        else
            sb.AppendLine("- (none)");

        if (!string.IsNullOrWhiteSpace(request.UseCase))
            sb.AppendLine($"\n## Use case: {request.UseCase}");

        if (request.Budget.HasValue)
            sb.AppendLine($"## Total budget: {request.Budget:F2}");

        sb.AppendLine();
        sb.AppendLine("## Missing categories to fill:");
        foreach (var m in missingCategories)
            sb.AppendLine($"- {m}");

        sb.AppendLine();
        sb.AppendLine("## Available in-stock products (choose ONE per missing category):");
        sb.AppendLine(JsonSerializer.Serialize(candidates, JsonOpts));

        sb.AppendLine();
        sb.AppendLine("## Instructions:");
        sb.AppendLine("- Respond ONLY with a valid JSON object — no markdown, no explanation outside JSON.");
        sb.AppendLine("- Use exactly this schema:");
        sb.AppendLine("""
{
  "recommendations": [
    {
      "category": "string",
      "productId": 0,
      "productName": "string",
      "price": 0.0,
      "reason": "string (max 80 chars)"
    }
  ],
  "explanation": "string (overall summary, max 200 chars)"
}
""");

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // LLM HTTP call (OpenAI-compatible endpoint)
    // -------------------------------------------------------------------------

    private async Task<string> CallLlmAsync(string prompt)
    {
        var apiKey = config["Llm:ApiKey"]
            ?? throw new InvalidOperationException("Llm:ApiKey is not configured.");
        var model = config["Llm:Model"] ?? "gpt-4o-mini";
        var endpoint = config["Llm:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var client = httpClientFactory.CreateClient("LlmClient");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await client.PostAsync(
            endpoint,
            new StringContent(json, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            logger.LogError("LLM API error {Status}: {Body}", response.StatusCode, error);
            throw new HttpRequestException($"LLM API returned {response.StatusCode}");
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? throw new InvalidOperationException("Empty response from LLM.");
    }

    // -------------------------------------------------------------------------
    // Response deserialization
    // -------------------------------------------------------------------------

    private RecommendationResponse DeserializeLlmResponse(string raw)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<LlmJsonResponse>(raw, JsonOpts)
                ?? throw new JsonException("Null deserialization result.");

            var recs = parsed.Recommendations?.Select(r => new RecommendedItem(
                r.Category ?? string.Empty,
                r.ProductId,
                r.ProductName ?? string.Empty,
                r.Price,
                r.Reason ?? string.Empty
            )).ToList() ?? [];

            return new RecommendationResponse(recs, parsed.Explanation ?? string.Empty);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse LLM JSON response: {Raw}", raw);
            return new RecommendationResponse([], $"Could not parse LLM response. Raw: {raw[..Math.Min(200, raw.Length)]}");
        }
    }

    // -------------------------------------------------------------------------
    // Internal DTOs for LLM JSON contract
    // -------------------------------------------------------------------------

    private sealed record CandidateInfo(
        int Id, string Name, decimal Price,
        string Category, string? Subcategory, string? Specifications);

    private sealed class LlmJsonResponse
    {
        [JsonPropertyName("recommendations")]
        public List<LlmRecommendedItem>? Recommendations { get; set; }

        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }
    }

    private sealed class LlmRecommendedItem
    {
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("productId")] public int ProductId { get; set; }
        [JsonPropertyName("productName")] public string? ProductName { get; set; }
        [JsonPropertyName("price")] public decimal Price { get; set; }
        [JsonPropertyName("reason")] public string? Reason { get; set; }
    }
}
