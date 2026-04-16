using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.DTOs;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

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
    private readonly IWebHostEnvironment _environment;

    private const LuceneVersion LuceneVer = LuceneVersion.LUCENE_48;
    private readonly RAMDirectory _directory = new();
    private volatile bool _indexed = false;
    private readonly SemaphoreSlim _buildLock = new(1, 1);

    public LuceneSearchService(
        IServiceScopeFactory scopeFactory,
        IWebHostEnvironment environment,
        ILogger<LuceneSearchService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
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
        var pdfMissingCount = 0;
        var pdfErrorCount = 0;

        foreach (var p in products)
        {
            var pdfRead = ExtractPdfText(p.PdfPath);
            if (pdfRead.State == PdfTextState.Missing)
                pdfMissingCount++;
            else if (pdfRead.State == PdfTextState.Error)
                pdfErrorCount++;

            var searchableContent = $"{p.Name} {pdfRead.Text}";

            var doc = new Document();
            doc.Add(new StringField("id",         p.Id.ToString(),             Field.Store.YES));
            doc.Add(new StringField("categoryId", p.CategoryId.ToString(),     Field.Store.YES));
            doc.Add(new TextField  ("name",        p.Name,                     Field.Store.YES));
            doc.Add(new TextField  ("pdf_full",    pdfRead.Text,               Field.Store.NO));
            doc.Add(new TextField  ("content",     searchableContent,          Field.Store.NO));
            doc.Add(new StoredField("price",       p.Price.ToString("F2")));
            doc.Add(new StoredField("stock",       p.StockQuantity.ToString()));
            doc.Add(new StoredField("categoryName",p.Category.Name));
            doc.Add(new StoredField("subcategoryName", p.Subcategory?.Name ?? ""));
            doc.Add(new StoredField("pdfPath", p.PdfPath ?? ""));
            writer.AddDocument(doc);
        }

        writer.Commit();
        _indexed = true;
        _logger.LogInformation(
            "Lucene index built with {Count} products. Missing PDFs: {MissingCount}. PDF read errors: {ErrorCount}.",
            products.Count,
            pdfMissingCount,
            pdfErrorCount);
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

            var parser = new MultiFieldQueryParser(
                LuceneVer,
                ["name", "pdf_full"],
                analyzer,
                new Dictionary<string, float>
                {
                    ["name"] = 2.0f,
                    ["pdf_full"] = 1.5f
                })
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
                luceneQuery = new TermQuery(new Term("pdf_full", query.ToLowerInvariant()));
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

            _logger.LogInformation(
                "Lucene spec search query='{Query}', categoryId={CategoryId}, maxResults={MaxResults}, hits={HitCount}",
                query,
                categoryId,
                maxResults,
                hits.TotalHits);

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

    private PdfTextReadResult ExtractPdfText(string? pdfPath)
    {
        var absolutePdfPath = ResolvePdfPath(pdfPath);
        if (absolutePdfPath is null || !File.Exists(absolutePdfPath))
            return new PdfTextReadResult(string.Empty, PdfTextState.Missing);

        try
        {
            using var document = PdfDocument.Open(absolutePdfPath);
            var words = new List<string>(1024);

            foreach (Page page in document.GetPages())
            {
                foreach (var word in page.GetWords())
                    words.Add(word.Text);
            }

            return new PdfTextReadResult(string.Join(' ', words), PdfTextState.Ok);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping PDF text extraction for {PdfPath}", pdfPath);
            return new PdfTextReadResult(string.Empty, PdfTextState.Error);
        }
    }

    private string? ResolvePdfPath(string? pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return null;

        var relativePath = pdfPath.TrimStart('~', '/', '\\').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_environment.WebRootPath, relativePath);
    }

    private enum PdfTextState { Ok, Missing, Error }

    private readonly record struct PdfTextReadResult(string Text, PdfTextState State);

    public void Dispose() => _directory.Dispose();
}
