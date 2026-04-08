using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.DTOs;
using Recomandare_PC.Models;

namespace Recomandare_PC.Services;

public interface IProductSearchService
{
    /// <summary>
    /// Searches products by name and specifications using BM25 scoring.
    /// Returns results sorted by relevance (descending).
    /// </summary>
    Task<IReadOnlyList<(ProductListDto Product, double Score)>> SearchAsync(
        string query,
        int maxResults = 20,
        int? categoryId = null);
}

public class ProductSearchService(AppDbContext db, ILogger<ProductSearchService> logger)
    : IProductSearchService
{
    // BM25 hyperparameters
    private const double K1 = 1.5;
    private const double B = 0.75;

    public async Task<IReadOnlyList<(ProductListDto Product, double Score)>> SearchAsync(
        string query,
        int maxResults = 20,
        int? categoryId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var queryTerms = Tokenize(query);
        if (queryTerms.Length == 0)
            return [];

        // Pull candidate products from DB (apply category filter early)
        IQueryable<Product> q = db.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .Where(p => p.StockQuantity > 0);

        if (categoryId.HasValue)
            q = q.Where(p => p.CategoryId == categoryId.Value);

        var products = await q.ToListAsync();

        if (products.Count == 0)
            return [];

        // Build corpus: one "document" per product (name + specifications)
        var corpus = products
            .Select(p => new
            {
                Product = p,
                Tokens = Tokenize(BuildDocument(p))
            })
            .ToList();

        int N = corpus.Count;
        double avgDocLen = corpus.Average(d => (double)d.Tokens.Length);

        // Precompute term → document frequency
        var df = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in corpus)
            foreach (var term in doc.Tokens.Distinct(StringComparer.OrdinalIgnoreCase))
                df[term] = df.GetValueOrDefault(term) + 1;

        // Score each document
        var scored = corpus.Select(doc =>
        {
            double score = 0;
            int docLen = doc.Tokens.Length;
            var tf = ComputeTf(doc.Tokens);

            foreach (var term in queryTerms)
            {
                if (!tf.TryGetValue(term, out int termFreq)) continue;
                if (!df.TryGetValue(term, out int docFreq)) continue;

                double idf = Math.Log((N - docFreq + 0.5) / (docFreq + 0.5) + 1);
                double tfNorm = (termFreq * (K1 + 1)) /
                                (termFreq + K1 * (1 - B + B * docLen / avgDocLen));
                score += idf * tfNorm;
            }

            return (doc.Product, Score: score);
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .Take(maxResults)
        .ToList();

        logger.LogDebug("BM25 search for '{Query}' returned {Count} results", query, scored.Count);

        return scored.Select(x => (MapToDto(x.Product), x.Score)).ToList();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string BuildDocument(Product p)
    {
        var sb = new System.Text.StringBuilder();
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
            .Split([' ', '\t', '\n', '\r', ',', '.', '/', '-', '_', '(', ')'],
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1)
            .ToArray();

    private static Dictionary<string, int> ComputeTf(string[] tokens)
    {
        var tf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tokens)
            tf[t] = tf.GetValueOrDefault(t) + 1;
        return tf;
    }

    private static ProductListDto MapToDto(Product p) => new(
        p.Id,
        p.Name,
        p.Price,
        p.StockQuantity,
        p.Category.Name,
        p.Subcategory?.Name
    );
}
