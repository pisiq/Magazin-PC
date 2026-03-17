using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.DTOs;
using System.Text.Json;

namespace Recomandare_PC.Services;

public interface ILuceneSearchService
{
    Task EnsureIndexAsync();
    Task RebuildIndexAsync();
    IReadOnlyList<(ProductListDto Product, float Score)> Search(string query, int maxResults = 20, int? categoryId = null);
}

public sealed class LuceneSearchService : ILuceneSearchService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LuceneSearchService> _logger;

    private const LuceneVersion LuceneVer = LuceneVersion.LUCENE_48;
    private readonly RAMDirectory _directory = new();
    private volatile bool _indexed = false;
    private readonly SemaphoreSlim _buildLock = new(1, 1);

    public LuceneSearchService(IServiceScopeFactory scopeFactory, ILogger<LuceneSearchService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task EnsureIndexAsync()
    {
        if (_indexed) return;
        await _buildLock.WaitAsync();
        try
        {
            if (!_indexed) await BuildCoreAsync();
        }
        finally
        {
            _buildLock.Release();
        }
    }

    public async Task RebuildIndexAsync()
    {
        await _buildLock.WaitAsync();
        try
        {
            await BuildCoreAsync();
        }
        finally
        {
            _buildLock.Release();
        }
    }

    private async Task BuildCoreAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var products = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .ToListAsync();

        var analyzer = new StandardAnalyzer(LuceneVer);
        var config = new IndexWriterConfig(LuceneVer, analyzer)
        {
            OpenMode = OpenMode.CREATE,
            Similarity = new BM25Similarity()
        };

        using var writer = new IndexWriter(_directory, config);

        foreach (var p in products)
        {
            var specsText = ExtractSpecsText(p.Specifications);

            var doc = new Document();
            doc.Add(new StringField("id",         p.Id.ToString(),             Field.Store.YES));
            doc.Add(new StringField("categoryId", p.CategoryId.ToString(),     Field.Store.YES));
            doc.Add(new TextField  ("name",        p.Name,                     Field.Store.YES));
            doc.Add(new TextField  ("specs",       specsText,                  Field.Store.NO));
            doc.Add(new TextField  ("content",     $"{p.Name} {specsText}",    Field.Store.NO));
            doc.Add(new StoredField("price",       p.Price.ToString("F2")));
            doc.Add(new StoredField("stock",       p.StockQuantity.ToString()));
            doc.Add(new StoredField("categoryName",p.Category.Name));
            doc.Add(new StoredField("subcategoryName", p.Subcategory?.Name ?? ""));
            writer.AddDocument(doc);
        }

        writer.Commit();
        _indexed = true;
        _logger.LogInformation("Lucene index built with {Count} products", products.Count);
    }

    public IReadOnlyList<(ProductListDto Product, float Score)> Search(
        string query, int maxResults = 20, int? categoryId = null)
    {
        if (!_indexed)
        {
            _logger.LogWarning("Lucene index not ready — returning empty results");
            return [];
        }

        maxResults = Math.Clamp(maxResults, 1, 100);

        try
        {
            var analyzer = new StandardAnalyzer(LuceneVer);
            using var reader  = DirectoryReader.Open(_directory);
            var searcher = new IndexSearcher(reader) { Similarity = new BM25Similarity() };

            var parser = new MultiFieldQueryParser(LuceneVer, ["name", "specs", "content"], analyzer)
            {
                DefaultOperator = QueryParserBase.OR_OPERATOR
            };

            Query luceneQuery;
            try
            {
                luceneQuery = parser.Parse(QueryParserBase.Escape(query.Trim()));
            }
            catch
            {
                luceneQuery = new TermQuery(new Term("content", query.ToLowerInvariant()));
            }

            if (categoryId.HasValue)
            {
                var filtered = new BooleanQuery();
                filtered.Add(luceneQuery, Occur.MUST);
                filtered.Add(new TermQuery(new Term("categoryId", categoryId.Value.ToString())), Occur.MUST);
                luceneQuery = filtered;
            }

            var hits    = searcher.Search(luceneQuery, maxResults);
            var results = new List<(ProductListDto, float)>(hits.ScoreDocs.Length);

            foreach (var hit in hits.ScoreDocs)
            {
                var doc = searcher.Doc(hit.Doc);
                var sub = doc.Get("subcategoryName");
                results.Add((new ProductListDto(
                    int.Parse(doc.Get("id")),
                    doc.Get("name"),
                    decimal.Parse(doc.Get("price")),
                    int.Parse(doc.Get("stock")),
                    doc.Get("categoryName"),
                    string.IsNullOrEmpty(sub) ? null : sub
                ), hit.Score));
            }

            return results.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lucene search failed for query: {Query}", query);
            return [];
        }
    }

    private static string ExtractSpecsText(string? specsJson)
    {
        if (string.IsNullOrWhiteSpace(specsJson)) return "";
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(specsJson);
            return dict is null ? specsJson : string.Join(" ", dict.Values);
        }
        catch
        {
            return specsJson;
        }
    }

    public void Dispose() => _directory.Dispose();
}
