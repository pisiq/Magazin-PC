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

    public SelectList CategoryOptions { get; set; } = null!;
    public string SubcategoryJson { get; set; } = "[]";

    public async Task OnGetAsync()
    {
        await PopulateSelectsAsync();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? PdfFile)
    {
        await PopulateSelectsAsync();

        if (!ModelState.IsValid) return Page();

        if (PdfFile is { Length: > 0 })
        {
            var pdfDir = Path.Combine(env.WebRootPath, "pdfs");
            Directory.CreateDirectory(pdfDir);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(PdfFile.FileName)}";
            var absolutePath = Path.Combine(pdfDir, fileName);

            await using (var fs = System.IO.File.Create(absolutePath))
                await PdfFile.CopyToAsync(fs);

            Product.PdfPath = $"pdfs/{fileName}";
            Product.Specifications = await pdfService.ExtractSpecificationsAsync(absolutePath);
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
