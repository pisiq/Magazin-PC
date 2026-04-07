using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Data;
using Recomandare_PC.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Logging: keep detailed diagnostics visible in terminal for API debugging ────
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "HH:mm:ss ";
    options.SingleLine = true;
});
builder.Logging.AddDebug();

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
builder.Services.AddScoped<ISimilarProductsService, SimilarProductsService>();
builder.Services.AddScoped<ILlmRecommendationService, LlmRecommendationService>();
builder.Services.AddSingleton<ILuceneSearchService, LuceneSearchService>();

// Named HttpClient for LLM calls
builder.Services.AddHttpClient("LlmClient");

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


app.UseStaticFiles();       // serves wwwroot/ including uploaded PDFs
app.UseSession();
app.UseRouting();

app.MapRazorPages();
app.MapControllers();

// MVC convention routing (Shop UI)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Shop}/{action=Search}/{id?}");

app.Run();
