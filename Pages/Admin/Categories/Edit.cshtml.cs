using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recomandare_PC.Data;
using Recomandare_PC.Models;

namespace Recomandare_PC.Pages.Admin.Categories;

public class EditModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Category Category { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var cat = await db.Categories.FindAsync(id);
        if (cat is null) return NotFound();
        Category = cat;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        db.Categories.Update(Category);
        await db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
