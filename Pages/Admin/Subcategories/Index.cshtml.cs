using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.Models;

namespace Recomandare_PC.Pages.Admin.Subcategories;

public class IndexModel(AppDbContext db) : PageModel
{
    public IList<Subcategory> Subcategories { get; set; } = [];

    public async Task OnGetAsync()
    {
        Subcategories = await db.Subcategories
            .Include(s => s.Category)
            .OrderBy(s => s.Category.Name).ThenBy(s => s.Name)
            .ToListAsync();
    }
}
