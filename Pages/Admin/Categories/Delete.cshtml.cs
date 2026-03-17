using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recomandare_PC.Data;
using Recomandare_PC.Models;

namespace Recomandare_PC.Pages.Admin.Categories;

public class DeleteModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Category? Category { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Category = await db.Categories.FindAsync(id);
        if (Category is null) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Category is null) return RedirectToPage("Index");

        var entity = await db.Categories.FindAsync(Category.Id);
        if (entity is not null)
        {
            db.Categories.Remove(entity);
            await db.SaveChangesAsync();
        }
        return RedirectToPage("Index");
    }
}
