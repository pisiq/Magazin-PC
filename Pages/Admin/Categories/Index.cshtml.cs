using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.Models;

namespace Recomandare_PC.Pages.Admin.Categories;

public class IndexModel(AppDbContext db) : PageModel
{
    public IList<Category> Categories { get; set; } = [];

    public async Task OnGetAsync()
    {
        Categories = await db.Categories
            .Include(c => c.Subcategories)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }
}
