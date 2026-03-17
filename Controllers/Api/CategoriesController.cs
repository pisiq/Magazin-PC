using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;

namespace Recomandare_PC.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController(AppDbContext db) : ControllerBase
{
    // GET api/categories
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        return Ok(categories);
    }

    // GET api/categories/{id}/subcategories
    [HttpGet("{id:int}/subcategories")]
    public async Task<IActionResult> GetSubcategories(int id)
    {
        var subs = await db.Subcategories
            .AsNoTracking()
            .Where(s => s.CategoryId == id)
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();

        return Ok(subs);
    }

    // GET api/categories/{id}/products
    [HttpGet("{id:int}/products")]
    public async Task<IActionResult> GetProducts(int id)
    {
        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.CategoryId == id && p.StockQuantity > 0)
            .OrderBy(p => p.Price)
            .Select(p => new { p.Id, p.Name, p.Price, p.StockQuantity })
            .ToListAsync();

        return Ok(products);
    }
}
