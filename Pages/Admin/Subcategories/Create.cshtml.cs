using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.Models;

namespace Recomandare_PC.Pages.Admin.Subcategories;

public class CreateModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Subcategory Subcategory { get; set; } = new();
    public SelectList CategoryOptions { get; set; } = null!;

    public async Task OnGetAsync()
    {
        var cats = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        CategoryOptions = new SelectList(cats, "Id", "Name");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var cats = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        CategoryOptions = new SelectList(cats, "Id", "Name");

        if (!ModelState.IsValid) return Page();

        db.Subcategories.Add(Subcategory);
        await db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
