using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content; // namespace stays the same inside the PdfPig package

namespace Recomandare_PC.Services;

public record PdfAnalysisResult(
    string Specifications,
    string? SuggestedCategoryName,
    string? SuggestedSubcategoryName);

public interface IPdfExtractionService
{
    /// <summary>
    /// Extracts text from a PDF file and returns a structured JSON string
    /// suitable for storing in Product.Specifications.
    /// </summary>
    Task<string> ExtractSpecificationsAsync(string absolutePdfPath);

    /// <summary>
    /// Extracts specifications AND detects the likely category/subcategory from the PDF text.
    /// </summary>
    Task<PdfAnalysisResult> AnalyzePdfAsync(string absolutePdfPath);
}

public class PdfExtractionService(ILogger<PdfExtractionService> logger) : IPdfExtractionService
{
    public Task<string> ExtractSpecificationsAsync(string absolutePdfPath)
    {
        if (!File.Exists(absolutePdfPath))
            throw new FileNotFoundException("PDF file not found.", absolutePdfPath);

        try
        {
            var rawText = ExtractRawText(absolutePdfPath);
            var specs = ParseSpecifications(rawText);
            return Task.FromResult(JsonSerializer.Serialize(specs, new JsonSerializerOptions { WriteIndented = false }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract specifications from {Path}", absolutePdfPath);
            return Task.FromResult(JsonSerializer.Serialize(new { raw_text = string.Empty, error = ex.Message }));
        }
    }

    public Task<PdfAnalysisResult> AnalyzePdfAsync(string absolutePdfPath)
    {
        if (!File.Exists(absolutePdfPath))
            throw new FileNotFoundException("PDF file not found.", absolutePdfPath);

        try
        {
            var rawText = ExtractRawText(absolutePdfPath);
            var specs = ParseSpecifications(rawText);
            var specsJson = JsonSerializer.Serialize(specs, new JsonSerializerOptions { WriteIndented = false });
            var (cat, sub) = DetectCategorySubcategory(rawText);
            return Task.FromResult(new PdfAnalysisResult(specsJson, cat, sub));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze PDF {Path}", absolutePdfPath);
            var fallback = JsonSerializer.Serialize(new { raw_text = string.Empty, error = ex.Message });
            return Task.FromResult(new PdfAnalysisResult(fallback, null, null));
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string ExtractRawText(string path)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(path);

        foreach (Page page in document.GetPages())
        {
            foreach (var word in page.GetWords())
                sb.Append(word.Text).Append(' ');
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Heuristic parser: looks for "Key: Value" or "Key – Value" patterns in the
    /// extracted text. Falls back to storing the full raw text when no pairs found.
    /// </summary>
    private static Dictionary<string, string> ParseSpecifications(string rawText)
    {
        var specs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var lines = rawText
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Try "Key: Value"
            var colonIdx = line.IndexOf(':');
            if (colonIdx > 1 && colonIdx < line.Length - 1)
            {
                var key = line[..colonIdx].Trim();
                var value = line[(colonIdx + 1)..].Trim();

                if (key.Length <= 60 && !string.IsNullOrWhiteSpace(value))
                {
                    specs[key] = value;
                    continue;
                }
            }

            // Try "Key – Value" or "Key - Value"
            var dashIdx = line.IndexOfAny(['-', '–']);
            if (dashIdx > 1 && dashIdx < line.Length - 1)
            {
                var key = line[..dashIdx].Trim();
                var value = line[(dashIdx + 1)..].Trim();

                if (key.Length <= 60 && !string.IsNullOrWhiteSpace(value))
                    specs.TryAdd(key, value);
            }
        }

        if (specs.Count == 0)
            specs["raw_text"] = rawText.Length > 4000 ? rawText[..4000] : rawText;

        return specs;
    }

    private static (string? category, string? subcategory) DetectCategorySubcategory(string rawText)
    {
        // CPU
        if (Has(rawText, "socket lga", "socket am5", "socket am4", "core i3", "core i5", "core i7", "core i9",
                         "ryzen", "threadripper", "xeon", "base clock", "boost clock", "l3 cache", "processor"))
            return ("CPU", null);

        // GPU
        if (Has(rawText, "graphics card", "vram", "cuda core", "geforce", "radeon", "rtx ", "gtx ",
                         "memory bandwidth", "shader", "gpu clock", "placa video"))
            return ("GPU", null);

        // RAM — subcategory from DDR generation
        if (Has(rawText, "ddr5"))
            return ("RAM", "DDR5");
        if (Has(rawText, "ddr4"))
            return ("RAM", "DDR4");
        if (Has(rawText, "dimm", "jedec", "xmp", "expo", "memory module", "memorie ram"))
            return ("RAM", null);

        // Storage — detect NVMe / SATA SSD / HDD
        if (Has(rawText, "nvme", "m.2 pcie", "pcie gen 4", "pcie gen 3"))
            return ("Storage", "SSD NVMe");
        if (Has(rawText, "ssd") && Has(rawText, "sata"))
            return ("Storage", "SSD SATA");
        if (Has(rawText, "hdd", "hard disk", "hard drive") || (Has(rawText, "7200 rpm") || Has(rawText, "5400 rpm")))
            return ("Storage", "HDD");
        if (Has(rawText, "sequential read", "sequential write", "tbw", "nand"))
            return ("Storage", null);

        // Motherboard
        if (Has(rawText, "motherboard", "placa de baza", "chipset", "vrm", "pcie x16", "dimm slot"))
            return ("Motherboard", null);

        // PSU
        if (Has(rawText, "power supply", "sursa de alimentare", "80plus", "80 plus", "modular", "atx12v", "watt"))
            return ("PSU", null);

        // Cooler — liquid vs air
        if (Has(rawText, "aio", "liquid cooling", "all-in-one cooler", "240mm", "280mm", "360mm", "water block", "racire lichid"))
            return ("Cooler", "Lichid");
        if (Has(rawText, "heatsink", "heat pipe", "air cooler", "tower cooler", "racire aer"))
            return ("Cooler", "Aer");
        if (Has(rawText, "cooler", "racire", "heat pipe"))
            return ("Cooler", null);

        // Case
        if (Has(rawText, "carcasa", "chassis", "mid tower", "full tower", "mini tower", "tempered glass", "pc case"))
            return ("Case", null);

        return (null, null);
    }

    private static bool Has(string text, params string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
