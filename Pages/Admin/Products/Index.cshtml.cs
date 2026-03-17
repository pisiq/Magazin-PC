using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.Models;

namespace Recomandare_PC.Pages.Admin.Products;

public class IndexModel(AppDbContext db) : PageModel
{
    public IList<Product> Products { get; set; } = [];

    public async Task OnGetAsync()
    {
        Products = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .OrderBy(p => p.CategoryId).ThenBy(p => p.Name)
            .ToListAsync();
    }
}
