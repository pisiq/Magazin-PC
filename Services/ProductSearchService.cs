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

    /// <summary>
    /// Predictive title search for autocomplete.
    /// Uses a hybrid score: prefix + trigram similarity + Levenshtein similarity.
    /// </summary>
    Task<IReadOnlyList<AutocompleteSuggestionDto>> GetAutocompleteAsync(
        string query,
        int maxResults = 8,
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

    public async Task<IReadOnlyList<AutocompleteSuggestionDto>> GetAutocompleteAsync(
        string query,
        int maxResults = 8,
        int? categoryId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        maxResults = Math.Clamp(maxResults, 1, 20);
        var normalizedQuery = query.Trim().ToLowerInvariant();
        if (normalizedQuery.Length < 2)
            return [];

        IQueryable<Product> q = db.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .AsNoTracking()
            .Where(p => p.StockQuantity > 0);

        if (categoryId.HasValue)
            q = q.Where(p => p.CategoryId == categoryId.Value);

        var products = await q.ToListAsync();
        if (products.Count == 0)
            return [];

        var scored = products
            .Select(p =>
            {
                var name = p.Name.ToLowerInvariant();
                var score = ComputeAutocompleteScore(normalizedQuery, name);
                return (Product: p, Score: score);
            })
            .Where(x => x.Score >= 0.15)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Product.Name)
            .Take(maxResults)
            .Select(x => new AutocompleteSuggestionDto(
                x.Product.Id,
                x.Product.Name,
                x.Product.Category.Name,
                x.Product.Subcategory?.Name,
                Math.Round(x.Score, 4)))
            .ToList();

        logger.LogDebug("Autocomplete for '{Query}' returned {Count} suggestions", query, scored.Count);

        return scored;
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

    private static double ComputeAutocompleteScore(string query, string productName)
    {
        double prefixScore = 0;
        if (productName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            prefixScore = 1;
        }
        else
        {
            var tokens = Tokenize(productName);
            if (tokens.Any(t => t.StartsWith(query, StringComparison.OrdinalIgnoreCase)))
                prefixScore = 0.75;
        }

        var trigramScore = TrigramJaccard(query, productName);
        var levenshteinScore = BestLevenshteinSimilarity(query, productName);

        return prefixScore * 0.55 + trigramScore * 0.30 + levenshteinScore * 0.15;
    }

    private static double TrigramJaccard(string a, string b)
    {
        var aSet = BuildTrigrams(a);
        var bSet = BuildTrigrams(b);
        if (aSet.Count == 0 || bSet.Count == 0)
            return 0;

        var intersection = aSet.Intersect(bSet).Count();
        var union = aSet.Count + bSet.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static HashSet<string> BuildTrigrams(string value)
    {
        var normalized = $"  {value.ToLowerInvariant()}  ";
        var result = new HashSet<string>(StringComparer.Ordinal);

        if (normalized.Length < 3)
        {
            result.Add(normalized);
            return result;
        }

        for (var i = 0; i <= normalized.Length - 3; i++)
            result.Add(normalized.Substring(i, 3));

        return result;
    }

    private static double BestLevenshteinSimilarity(string query, string productName)
    {
        var candidates = Tokenize(productName).Append(productName);
        var best = 0.0;

        foreach (var candidate in candidates)
        {
            var dist = LevenshteinDistance(query, candidate);
            var maxLen = Math.Max(query.Length, candidate.Length);
            if (maxLen == 0) continue;

            var sim = 1.0 - (double)dist / maxLen;
            if (sim > best) best = sim;
        }

        return best;
    }

    private static int LevenshteinDistance(string source, string target)
    {
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        var rows = source.Length + 1;
        var cols = target.Length + 1;
        var dp = new int[rows, cols];

        for (var i = 0; i < rows; i++) dp[i, 0] = i;
        for (var j = 0; j < cols; j++) dp[0, j] = j;

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[rows - 1, cols - 1];
    }
}
