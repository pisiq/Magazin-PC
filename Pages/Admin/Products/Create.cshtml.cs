using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.Models;
using Recomandare_PC.Services;

namespace Recomandare_PC.Pages.Admin.Products;

public class CreateModel(
    AppDbContext db,
    IWebHostEnvironment env,
    IPdfExtractionService pdfService,
    ILuceneSearchService lucene) : PageModel
{
    [BindProperty]
    public Product Product { get; set; } = new();

    [BindProperty]
    public bool PdfAnalysisCompleted { get; set; }

    [BindProperty]
    public bool ConfirmAutoExtractedData { get; set; }

    public SelectList CategoryOptions { get; set; } = null!;
    public string SubcategoryJson { get; set; } = "[]";

    public async Task OnGetAsync()
    {
        await PopulateSelectsAsync();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? pdfFile)
    {
        await PopulateSelectsAsync();

        if (pdfFile is { Length: > 0 })
        {
            if (!PdfAnalysisCompleted)
                ModelState.AddModelError(string.Empty, "Mai întâi analizează PDF-ul pentru a completa automat datele produsului.");

            if (!ConfirmAutoExtractedData)
                ModelState.AddModelError(string.Empty, "Confirmă datele extrase automat din PDF înainte de salvare.");
        }

        if (!ModelState.IsValid)
            return Page();

        if (pdfFile is { Length: > 0 })
        {
            var pdfDir = Path.Combine(env.WebRootPath, "pdfs");
            Directory.CreateDirectory(pdfDir);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(pdfFile.FileName)}";
            var absolutePath = Path.Combine(pdfDir, fileName);

            await using (var fs = System.IO.File.Create(absolutePath))
                await pdfFile.CopyToAsync(fs);

            Product.PdfPath = $"pdfs/{fileName}";
            var analysis = await pdfService.AnalyzePdfAsync(absolutePath);
            Product.Specifications = analysis.Specifications;

            if (string.IsNullOrWhiteSpace(Product.Name) && !string.IsNullOrWhiteSpace(analysis.SuggestedProductName))
                Product.Name = analysis.SuggestedProductName;

            if (Product.CategoryId == default && !string.IsNullOrWhiteSpace(analysis.SuggestedCategoryName))
            {
                var detectedCategory = await db.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Name == analysis.SuggestedCategoryName);

                if (detectedCategory is not null)
                {
                    Product.CategoryId = detectedCategory.Id;

                    if (Product.SubcategoryId is null && !string.IsNullOrWhiteSpace(analysis.SuggestedSubcategoryName))
                    {
                        var detectedSubcategory = await db.Subcategories
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s =>
                                s.CategoryId == detectedCategory.Id &&
                                s.Name == analysis.SuggestedSubcategoryName);

                        if (detectedSubcategory is not null)
                            Product.SubcategoryId = detectedSubcategory.Id;
                    }
                }
            }
        }

        db.Products.Add(Product);
        await db.SaveChangesAsync();
        await lucene.RebuildIndexAsync();
        return RedirectToPage("Index");
    }

    private async Task PopulateSelectsAsync()
    {
        var cats = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        CategoryOptions = new SelectList(cats, "Id", "Name");

        var subs = await db.Subcategories.OrderBy(s => s.Name).ToListAsync();
        SubcategoryJson = JsonSerializer.Serialize(
            subs.Select(s => new { s.Id, s.Name, s.CategoryId }));
    }
}
