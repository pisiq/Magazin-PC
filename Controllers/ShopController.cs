using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.Models;
using Recomandare_PC.Services;
using System.Text.Json;

namespace Recomandare_PC.Controllers;

public class ShopController(
    AppDbContext db,
    ILuceneSearchService luceneSearch,
    IGeminiRecommendationService geminiService) : Controller
{
    private const string CartKey = "Cart";

    // ── Cart helpers ─────────────────────────────────────────────────────────────

    private List<CartItem> GetCart()
    {
        var json = HttpContext.Session.GetString(CartKey);
        return json is null ? [] : JsonSerializer.Deserialize<List<CartItem>>(json) ?? [];
    }

    private void SaveCart(List<CartItem> cart) =>
        HttpContext.Session.SetString(CartKey, JsonSerializer.Serialize(cart));

    // ── Search ───────────────────────────────────────────────────────────────────

    // GET /Shop/Search
    public async Task<IActionResult> Search(string? q, int? categoryId)
    {
        await luceneSearch.EnsureIndexAsync();

        var results = string.IsNullOrWhiteSpace(q)
            ? []
            : luceneSearch.Search(q, 30, categoryId);

        ViewBag.Query      = q ?? "";
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.CartCount  = GetCart().Count;

        return View(results);
    }

    // ── Cart ─────────────────────────────────────────────────────────────────────

    // POST /Shop/AddToCart
    [HttpPost]
    public async Task<IActionResult> AddToCart(int productId, string? returnUrl)
    {
        var product = await db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product is null) return NotFound();

        var cart = GetCart();
        if (!cart.Any(c => c.ProductId == productId))
            cart.Add(new CartItem(product.Id, product.Name, product.Category.Name, product.Price));

        SaveCart(cart);
        TempData["Message"] = $"'{product.Name}' a fost adăugat în coș.";

        return Redirect(returnUrl ?? Url.Action(nameof(Search))!);
    }

    // POST /Shop/RemoveFromCart
    [HttpPost]
    public IActionResult RemoveFromCart(int productId)
    {
        var cart = GetCart();
        cart.RemoveAll(c => c.ProductId == productId);
        SaveCart(cart);

        return RedirectToAction(nameof(Checkout));
    }

    // ── Checkout ─────────────────────────────────────────────────────────────────

    // GET /Shop/Checkout
    public async Task<IActionResult> Checkout()
    {
        var cart = GetCart();

        GeminiRecommendationResult? recommendations = null;
        if (cart.Count > 0)
            recommendations = await geminiService.RecommendAsync(cart);

        ViewBag.CartCount       = cart.Count;
        ViewBag.Recommendations = recommendations;

        return View(cart);
    }

    // POST /Shop/FinalizeOrder
    [HttpPost]
    public IActionResult FinalizeOrder()
    {
        SaveCart([]);
        TempData["OrderConfirmed"] = true;

        return RedirectToAction(nameof(Checkout));
    }
}
