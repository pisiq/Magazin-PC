using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recomandare_PC.Data;
using Recomandare_PC.Models;
using Recomandare_PC.Services;

namespace Recomandare_PC.Pages.Admin.Products;

public class DeleteModel(AppDbContext db, ILuceneSearchService lucene) : PageModel
{
    [BindProperty]
    public Product? Product { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Product = await db.Products.FindAsync(id);
        if (Product is null) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Product is null) return RedirectToPage("Index");

        var entity = await db.Products.FindAsync(Product.Id);
        if (entity is not null)
        {
            db.Products.Remove(entity);
            await db.SaveChangesAsync();
            await lucene.RebuildIndexAsync();
        }
        return RedirectToPage("Index");
    }
}
