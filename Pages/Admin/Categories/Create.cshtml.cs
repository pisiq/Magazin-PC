using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recomandare_PC.Data;
using Recomandare_PC.Models;

namespace Recomandare_PC.Pages.Admin.Categories;

public class CreateModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Category Category { get; set; } = new();

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        db.Categories.Add(Category);
        await db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
