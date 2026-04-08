using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.DTOs;
using Recomandare_PC.Services;

namespace Recomandare_PC.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(
    AppDbContext db,
    IProductSearchService searchService,
    IPdfExtractionService pdfService) : ControllerBase
{
    // GET api/products
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var q = db.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .AsNoTracking();

        if (categoryId.HasValue)
            q = q.Where(p => p.CategoryId == categoryId.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(p => p.CategoryId).ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListDto(
                p.Id, p.Name, p.Price, p.StockQuantity,
                p.Category.Name, p.Subcategory == null ? null : p.Subcategory.Name))
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    // GET api/products/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var p = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (p is null) return NotFound();

        return Ok(new ProductDto(
            p.Id, p.Name, p.Price, p.StockQuantity,
            p.Category.Name, p.Subcategory?.Name, p.Specifications));
    }

    // POST api/products/analyze-pdf
    [HttpPost("analyze-pdf")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AnalyzePdf(IFormFile? pdfFile)
    {
        if (pdfFile is not { Length: > 0 })
            return BadRequest("No PDF file provided.");

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
        try
        {
            await using (var fs = System.IO.File.Create(tempFile))
                await pdfFile.CopyToAsync(fs);

            var result = await pdfService.AnalyzePdfAsync(tempFile);

            int? categoryId = null;
            int? subcategoryId = null;

            if (result.SuggestedCategoryName is not null)
            {
                var cat = await db.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Name == result.SuggestedCategoryName);
                categoryId = cat?.Id;

                if (categoryId.HasValue && result.SuggestedSubcategoryName is not null)
                {
                    var sub = await db.Subcategories
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.CategoryId == categoryId.Value
                                               && s.Name == result.SuggestedSubcategoryName);
                    subcategoryId = sub?.Id;
                }
            }

            return Ok(new
            {
                specs = result.Specifications,
                categoryId,
                subcategoryId,
                suggestedCategoryName = result.SuggestedCategoryName,
                suggestedSubcategoryName = result.SuggestedSubcategoryName
            });
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
                System.IO.File.Delete(tempFile);
        }
    }

    // GET api/products/search?q=ryzen&categoryId=1
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int? categoryId,
        [FromQuery] int top = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Query parameter 'q' is required.");

        var results = await searchService.SearchAsync(q, top, categoryId);
        return Ok(results.Select(r => new { r.Product, r.Score }));
    }
}
