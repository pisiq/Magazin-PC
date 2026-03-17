using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.Models;

namespace Recomandare_PC.Pages.Admin.Subcategories;

public class EditModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Subcategory Subcategory { get; set; } = new();
    public SelectList CategoryOptions { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var sub = await db.Subcategories.FindAsync(id);
        if (sub is null) return NotFound();
        Subcategory = sub;
        await PopulateSelectsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await PopulateSelectsAsync();
        if (!ModelState.IsValid) return Page();
        db.Subcategories.Update(Subcategory);
        await db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task PopulateSelectsAsync()
    {
        var cats = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        CategoryOptions = new SelectList(cats, "Id", "Name");
    }
}
