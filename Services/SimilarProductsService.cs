using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.DTOs;
using Recomandare_PC.Models;

namespace Recomandare_PC.Services;

public interface ISimilarProductsService
{
    Task<IReadOnlyList<(ProductListDto Product, double Score)>> GetSimilarProductsAsync(int productId, int top = 6);
}

public class SimilarProductsService(AppDbContext db, ILogger<SimilarProductsService> logger) : ISimilarProductsService
{
    public async Task<IReadOnlyList<(ProductListDto Product, double Score)>> GetSimilarProductsAsync(int productId, int top = 6)
    {
        top = Math.Clamp(top, 1, 24);

        var products = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .AsNoTracking()
            .Where(p => p.StockQuantity > 0)
            .ToListAsync();

        var target = products.FirstOrDefault(p => p.Id == productId);
        if (target is null)
            return [];

        var corpus = products.Select(p => new
        {
            Product = p,
            Tokens = Tokenize(BuildDocument(p))
        }).ToList();

        if (corpus.Count < 2)
            return [];

        var df = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in corpus)
        {
            foreach (var term in doc.Tokens.Distinct(StringComparer.OrdinalIgnoreCase))
                df[term] = df.GetValueOrDefault(term) + 1;
        }

        var vectors = corpus.ToDictionary(
            c => c.Product.Id,
            c => BuildTfidfVector(c.Tokens, df, corpus.Count));

        if (!vectors.TryGetValue(productId, out var targetVector))
            return [];

        var similar = corpus
            .Where(c => c.Product.Id != productId)
            .Select(c =>
            {
                var candidateVector = vectors[c.Product.Id];
                var score = CosineSimilarity(targetVector, candidateVector);

                // Small domain boost for same category.
                if (c.Product.CategoryId == target.CategoryId)
                    score *= 1.1;

                return (c.Product, Score: score);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(top)
            .Select(x => (MapToDto(x.Product), Math.Round(x.Score, 4)))
            .ToList();

        logger.LogDebug("Similarity for product {ProductId} returned {Count} items", productId, similar.Count);

        return similar;
    }

    private static string BuildDocument(Product p)
    {
        var sb = new StringBuilder();
        sb.Append(p.Name).Append(' ');

        if (!string.IsNullOrWhiteSpace(p.Specifications))
        {
            try
            {
                using var doc = JsonDocument.Parse(p.Specifications);
                foreach (var prop in doc.RootElement.EnumerateObject())
                    sb.Append(prop.Name).Append(' ').Append(prop.Value).Append(' ');
            }
            catch
            {
                sb.Append(p.Specifications);
            }
        }

        return sb.ToString();
    }

    private static string[] Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split([' ', '\t', '\n', '\r', ',', '.', '/', '-', '_', '(', ')', ':'],
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1)
            .ToArray();

    private static Dictionary<string, double> BuildTfidfVector(
        string[] tokens,
        Dictionary<string, int> df,
        int totalDocs)
    {
        var tf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
            tf[token] = tf.GetValueOrDefault(token) + 1;

        var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in tf)
        {
            if (!df.TryGetValue(kvp.Key, out var docFreq))
                continue;

            var tfNorm = (double)kvp.Value / tokens.Length;
            var idf = Math.Log((double)(1 + totalDocs) / (1 + docFreq)) + 1;
            vector[kvp.Key] = tfNorm * idf;
        }

        return vector;
    }

    private static double CosineSimilarity(
        Dictionary<string, double> left,
        Dictionary<string, double> right)
    {
        if (left.Count == 0 || right.Count == 0)
            return 0;

        var dot = 0.0;
        foreach (var kvp in left)
        {
            if (right.TryGetValue(kvp.Key, out var rightWeight))
                dot += kvp.Value * rightWeight;
        }

        var leftNorm = Math.Sqrt(left.Values.Sum(v => v * v));
        var rightNorm = Math.Sqrt(right.Values.Sum(v => v * v));

        if (leftNorm == 0 || rightNorm == 0)
            return 0;

        return dot / (leftNorm * rightNorm);
    }

    private static ProductListDto MapToDto(Product p) => new(
        p.Id,
        p.Name,
        p.Price,
        p.StockQuantity,
        p.Category.Name,
        p.Subcategory?.Name);
}

