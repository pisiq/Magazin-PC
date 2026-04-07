using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.DTOs;
using Recomandare_PC.Models;
using Recomandare_PC.Services;
using System.Text.Json;

namespace Recomandare_PC.Controllers;

public class ShopController(
    AppDbContext db,
    IProductSearchService productSearch,
    ISimilarProductsService similarProductsService,
    ILlmRecommendationService llmRecommendationService,
    ILogger<ShopController> logger) : Controller
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
        var results = string.IsNullOrWhiteSpace(q)
            ? []
            : await productSearch.SearchAsync(q, 30, categoryId);

        ViewBag.Query      = q ?? "";
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.CartCount  = GetCart().Count;

        return View(results);
    }

    // GET /Shop/Product/{id}
    public async Task<IActionResult> Product(int id)
    {
        var product = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Subcategory)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
            return NotFound();

        var specs = ParseSpecifications(product.Specifications);
        var similar = await similarProductsService.GetSimilarProductsAsync(product.Id, 6);

        ViewBag.CartCount = GetCart().Count;

        return View(new ProductDetailsViewModel
        {
            Product = new ProductDto(
                product.Id,
                product.Name,
                product.Price,
                product.StockQuantity,
                product.Category.Name,
                product.Subcategory?.Name,
                product.Specifications),
            Specifications = specs,
            SimilarProducts = similar
        });
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

    // GET /Shop/BuildAssistant
    public IActionResult BuildAssistant()
    {
        ViewBag.CartCount = GetCart().Count;
        return View(new BuildAssistantViewModel());
    }

    // POST /Shop/BuildAssistant
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuildAssistant(BuildAssistantViewModel model)
    {
        ViewBag.CartCount = GetCart().Count;

        try
        {
            var request = model.ToRequestDto().ToRecommendationRequest();
            model.Recommendation = await llmRecommendationService.RecommendAsync(request);
        }
        catch (Exception ex)
        {
            model.ErrorMessage = "Nu am putut genera recomandarile in acest moment. Incearca din nou.";
            logger.LogError(ex, "BuildAssistant recommendation failed");
        }

        return View(model);
    }

    // GET /Shop/Checkout
    public IActionResult Checkout()
    {
        var cart = GetCart();

        ViewBag.CartCount = cart.Count;

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

    private static IReadOnlyList<KeyValuePair<string, string>> ParseSpecifications(string? specificationsJson)
    {
        if (string.IsNullOrWhiteSpace(specificationsJson))
            return [];

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(specificationsJson);
            if (dict is null || dict.Count == 0)
                return [];

            return dict.ToList();
        }
        catch
        {
            return [new KeyValuePair<string, string>("Detalii", specificationsJson)];
        }
    }
}
