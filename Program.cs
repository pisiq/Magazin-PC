using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Razor Pages (Admin UI) ────────────────────────────────────────────────────
builder.Services.AddRazorPages();

// ── MVC Controllers + Views (Shop UI) ────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

// ── Session (cart) ────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IPdfExtractionService, PdfExtractionService>();
builder.Services.AddScoped<IProductSearchService, ProductSearchService>();
builder.Services.AddScoped<ILlmRecommendationService, LlmRecommendationService>();
builder.Services.AddSingleton<ILuceneSearchService, LuceneSearchService>();
builder.Services.AddScoped<IGeminiRecommendationService, GeminiRecommendationService>();

// Named HttpClient for LLM calls
builder.Services.AddHttpClient("LlmClient");
builder.Services.AddHttpClient("GeminiClient");

// ── Network: listen on all interfaces so LAN clients can reach the server ─────
builder.WebHost.UseUrls(
    builder.Configuration["Server:Url"] ?? "http://0.0.0.0:5000");

var app = builder.Build();

// ── Auto-migrate & seed on startup ───────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await DataSeeder.SeedProductsAsync(db);
}

// ── Build Lucene index on startup ─────────────────────────────────────────────
var lucene = app.Services.GetRequiredService<ILuceneSearchService>();
await lucene.RebuildIndexAsync();

app.UseStaticFiles();       // serves wwwroot/ including uploaded PDFs
app.UseSession();
app.UseRouting();

app.MapRazorPages();
app.MapControllers();

// MVC convention routing (Shop UI)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
