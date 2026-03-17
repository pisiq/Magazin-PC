using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recomandare_PC.Data;
using Recomandare_PC.Models;

namespace Recomandare_PC.Pages.Admin.Subcategories;

public class DeleteModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Subcategory? Subcategory { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Subcategory = await db.Subcategories.FindAsync(id);
        if (Subcategory is null) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Subcategory is null) return RedirectToPage("Index");
        var entity = await db.Subcategories.FindAsync(Subcategory.Id);
        if (entity is not null) { db.Subcategories.Remove(entity); await db.SaveChangesAsync(); }
        return RedirectToPage("Index");
    }
}
