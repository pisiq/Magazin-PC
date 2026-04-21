using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content; // namespace stays the same inside the PdfPig package

namespace Recomandare_PC.Services;

public record PdfAnalysisResult(
    string Specifications,
    string? SuggestedProductName,
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
            var parsed = ParseStructuredContent(rawText);
            return Task.FromResult(JsonSerializer.Serialize(parsed.Specifications, new JsonSerializerOptions { WriteIndented = false }));
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
            var parsed = ParseStructuredContent(rawText);
            var specsJson = JsonSerializer.Serialize(parsed.Specifications, new JsonSerializerOptions { WriteIndented = false });
            return Task.FromResult(new PdfAnalysisResult(specsJson, parsed.SuggestedProductName, parsed.SuggestedCategoryName, parsed.SuggestedSubcategoryName));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze PDF {Path}", absolutePdfPath);
            var fallback = JsonSerializer.Serialize(new { raw_text = string.Empty, error = ex.Message });
            return Task.FromResult(new PdfAnalysisResult(fallback, null, null, null));
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

    private sealed record ParsedPdfContent(
        Dictionary<string, string> Specifications,
        string? SuggestedProductName,
        string? SuggestedCategoryName,
        string? SuggestedSubcategoryName);

    private static ParsedPdfContent ParseStructuredContent(string rawText)
    {
        var specs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? suggestedName = null;
        string? suggestedCategory = null;
        string? suggestedSubcategory = null;
        var normalizedText = NormalizeForMatching(rawText);

        foreach (var (keyCandidate, valueCandidate) in ExtractOrderedKeyValuePairs(rawText))
        {
            var key = CleanLine(keyCandidate);
            var value = NormalizeSpecValue(valueCandidate);
            if (value.Length < 1)
                continue;

            var normalizedKey = NormalizeForMatching(key);

            if (IsNameKey(normalizedKey))
            {
                suggestedName ??= value;
                continue;
            }

            if (IsCategoryKey(normalizedKey))
            {
                suggestedCategory ??= CanonicalizeCategoryName(value) ?? value;
                continue;
            }

            if (IsTypeKey(normalizedKey))
            {
                suggestedSubcategory ??= CanonicalizeSubcategoryName(value) ?? value;
                continue;
            }

            var specKey = NormalizeSpecKey(key);
            if (specKey is null)
                continue;

            if (value.Length < 2)
                continue;

            specs[specKey] = value;
        }

        if (string.IsNullOrWhiteSpace(suggestedCategory))
        {
            var (detectedCategory, detectedSubcategory) = DetectCategorySubcategory(normalizedText);
            suggestedCategory = detectedCategory;
            suggestedSubcategory ??= detectedSubcategory;
        }
        else
        {
            suggestedCategory = CanonicalizeCategoryName(suggestedCategory) ?? suggestedCategory;
            suggestedSubcategory = CanonicalizeSubcategoryForCategory(suggestedSubcategory, suggestedCategory);
        }

        if (specs.Count == 0)
            specs["raw_text"] = rawText.Length > 8000 ? rawText[..8000] : rawText;

        return new ParsedPdfContent(specs, suggestedName, suggestedCategory, suggestedSubcategory);
    }

    private static IEnumerable<(string Key, string Value)> ExtractOrderedKeyValuePairs(string rawText)
    {
        var extracted = new List<(string Key, string Value)>();

        var lines = rawText
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanLine)
            .Where(l => l.Length > 2);

        foreach (var line in lines)
        {
            if (TryExtractPair(line, out var key, out var value))
                extracted.Add((key, value));
        }

        if (extracted.Count >= 2)
            return extracted;

        var labeledPairs = ExtractKnownLabeledPairs(rawText);
        if (labeledPairs.Count >= 2)
            return labeledPairs;

        return ExtractFlattenedPairsWithBacktracking(rawText);
    }

    private static readonly string[] KnownLabels =
    [
        "Name",
        "Product Name",
        "Model",
        "Category",
        "Type",
        "Socket",
        "RAM Support",
        "RAM",
        "PCIe",
        "Form Factor",
        "Cores",
        "Threads",
        "Base Clock",
        "Boost Clock",
        "Cache",
        "TDP",
        "VRAM",
        "Power",
        "Interface",
        "Chipset",
        "Capacity",
        "Speed",
        "Latency",
        "Wattage"
    ];

    private static List<(string Key, string Value)> ExtractKnownLabeledPairs(string rawText)
    {
        var flattened = Regex.Replace(rawText, @"\s+", " ").Trim();
        if (flattened.Length == 0)
            return [];

        var escapedLabels = KnownLabels
            .OrderByDescending(l => l.Length)
            .Select(Regex.Escape);

        var pattern = $@"(?<key>{string.Join("|", escapedLabels)})\s*:\s*";
        var matches = Regex.Matches(flattened, pattern, RegexOptions.IgnoreCase);
        if (matches.Count == 0)
            return [];

        var pairs = new List<(string Key, string Value)>();
        for (var i = 0; i < matches.Count; i++)
        {
            var current = matches[i];
            var key = CleanLine(current.Groups["key"].Value);

            var valueStart = current.Index + current.Length;
            var valueEnd = i + 1 < matches.Count ? matches[i + 1].Index : flattened.Length;
            if (valueStart >= valueEnd)
                continue;

            var value = CleanLine(flattened[valueStart..valueEnd]);
            if (key.Length is < 2 or > 80 || value.Length < 1)
                continue;

            pairs.Add((key, value));
        }

        return pairs;
    }

    private static List<(string Key, string Value)> ExtractFlattenedPairsWithBacktracking(string rawText)
    {
        var pairs = new List<(string Key, string Value)>();
        var flattened = Regex.Replace(rawText, @"\s+", " ").Trim();
        if (flattened.Length == 0)
            return pairs;

        var tokens = flattened.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var keyTokenIndexes = new List<int>();

        for (var i = 0; i < tokens.Length; i++)
        {
            if (IsKeyMarkerToken(tokens[i]))
                keyTokenIndexes.Add(i);
        }

        if (keyTokenIndexes.Count == 0)
            return pairs;

        for (var markerPos = 0; markerPos < keyTokenIndexes.Count; markerPos++)
        {
            var keyEndIndex = keyTokenIndexes[markerPos];
            var keyStartIndex = FindKeyStartIndex(tokens, keyEndIndex);

            var rawKeyTail = tokens[keyEndIndex];
            var keyTail = rawKeyTail[..rawKeyTail.IndexOf(':')];
            if (string.IsNullOrWhiteSpace(keyTail))
                continue;

            var keyParts = tokens[keyStartIndex..keyEndIndex].ToList();
            keyParts.Add(keyTail);
            var key = CleanLine(string.Join(" ", keyParts));

            var valueStartIndex = keyEndIndex + 1;
            var valueEndIndexExclusive = markerPos + 1 < keyTokenIndexes.Count
                ? keyTokenIndexes[markerPos + 1] - FindBacktrackLength(tokens, keyTokenIndexes[markerPos + 1])
                : tokens.Length;

            if (valueStartIndex >= valueEndIndexExclusive)
                continue;

            var value = CleanLine(string.Join(" ", tokens[valueStartIndex..valueEndIndexExclusive]));
            if (key.Length is < 2 or > 80 || value.Length < 1)
                continue;

            pairs.Add((key, value));
        }

        return pairs;
    }

    private static bool IsKeyMarkerToken(string token)
    {
        var colonIndex = token.IndexOf(':');
        if (colonIndex < 1)
            return false;

        var keyPart = token[..colonIndex];
        return Regex.IsMatch(keyPart, @"^[A-Za-z][A-Za-z0-9\-\+\.\(\)\/]*$");
    }

    private static int FindKeyStartIndex(string[] tokens, int keyEndIndex)
    {
        var backtrack = FindBacktrackLength(tokens, keyEndIndex);
        return Math.Max(0, keyEndIndex - backtrack);
    }

    private static int FindBacktrackLength(string[] tokens, int keyEndIndex)
    {
        var tailToken = tokens[keyEndIndex];
        var tail = tailToken[..tailToken.IndexOf(':')];
        var normalizedTail = NormalizeForMatching(tail);

        // Well-known labels should remain single-word keys (e.g. Category:),
        // otherwise names like "MSI ... Category:" could corrupt the key.
        if (normalizedTail is "name" or "category" or "type" or "model")
            return 0;

        var backtrack = 0;
        for (var i = keyEndIndex - 1; i >= 0 && backtrack < 2; i--)
        {
            var current = tokens[i];
            if (current.Contains(':'))
                break;

            if (!Regex.IsMatch(current, @"^[A-Za-z][A-Za-z0-9\-\+\.\(\)\/]*$"))
                break;

            backtrack++;
        }

        return backtrack;
    }

    private static bool IsNameKey(string normalizedKey)
        => normalizedKey is "name" or "product name" or "model" or "model name";

    private static bool IsCategoryKey(string normalizedKey)
        => normalizedKey is "category";

    private static bool IsTypeKey(string normalizedKey)
        => normalizedKey is "type" or "subtype" or "subcategory" or "sub category";

    private static string? CanonicalizeCategoryName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = NormalizeForMatching(value);
        normalized = Regex.Replace(normalized, @"[^a-z0-9 ]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized switch
        {
            "cpu" or "processor" or "procesor" => "CPU",
            "gpu" or "graphics card" or "video card" or "placa video" => "GPU",
            "ram" or "memory" or "memorie ram" => "RAM",
            "motherboard" or "mainboard" or "placa de baza" => "Motherboard",
            "psu" or "power supply" or "power supply unit" or "sursa" or "sursa de alimentare" => "PSU",
            "storage" or "stocare" or "drive" => "Storage",
            "cooler" or "cpu cooler" or "cooling" or "racire" => "Cooler",
            "case" or "pc case" or "chassis" or "carcasa" => "Case",
            _ => null
        };
    }

    private static string? CanonicalizeSubcategoryName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = NormalizeForMatching(value);
        normalized = Regex.Replace(normalized, @"[^a-z0-9 ]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized switch
        {
            "ddr4" => "DDR4",
            "ddr5" => "DDR5",
            "ssd nvme" or "nvme" or "m2 nvme" => "SSD NVMe",
            "ssd sata" or "sata ssd" => "SSD SATA",
            "hdd" or "hard disk" or "hard drive" => "HDD",
            "air" or "air cooler" or "aer" => "Aer",
            "liquid" or "aio" or "water" or "lichid" or "liquid cooling" => "Lichid",
            _ => null
        };
    }

    private static string? CanonicalizeSubcategoryForCategory(string? suggestedSubcategory, string? suggestedCategory)
    {
        if (string.IsNullOrWhiteSpace(suggestedSubcategory) || string.IsNullOrWhiteSpace(suggestedCategory))
            return suggestedSubcategory;

        var canonicalSub = CanonicalizeSubcategoryName(suggestedSubcategory) ?? suggestedSubcategory;
        var canonicalCategory = CanonicalizeCategoryName(suggestedCategory) ?? suggestedCategory;

        return canonicalCategory switch
        {
            "RAM" when canonicalSub is "DDR4" or "DDR5" => canonicalSub,
            "Storage" when canonicalSub is "SSD NVMe" or "SSD SATA" or "HDD" => canonicalSub,
            "Cooler" when canonicalSub is "Aer" or "Lichid" => canonicalSub,
            _ => null
        };
    }

    private static (string? category, string? subcategory) DetectCategorySubcategory(string normalizedText)
    {
        // CPU
        if (Has(normalizedText, "socket lga", "socket am5", "socket am4", "core i3", "core i5", "core i7", "core i9",
                         "ryzen", "threadripper", "xeon", "base clock", "boost clock", "l3 cache", "processor"))
            return ("CPU", null);

        // GPU
        if (Has(normalizedText, "graphics card", "vram", "cuda core", "geforce", "radeon", "rtx ", "gtx ",
                         "memory bandwidth", "shader", "gpu clock", "placa video"))
            return ("GPU", null);

        // RAM — subcategory from DDR generation
        if (Has(normalizedText, "ddr5"))
            return ("RAM", "DDR5");
        if (Has(normalizedText, "ddr4"))
            return ("RAM", "DDR4");
        if (Has(normalizedText, "dimm", "jedec", "xmp", "expo", "memory module", "memorie ram"))
            return ("RAM", null);

        // Storage — detect NVMe / SATA SSD / HDD
        if (Has(normalizedText, "nvme", "m.2 pcie", "pcie gen 4", "pcie gen 3"))
            return ("Storage", "SSD NVMe");
        if (Has(normalizedText, "ssd") && Has(normalizedText, "sata"))
            return ("Storage", "SSD SATA");
        if (Has(normalizedText, "hdd", "hard disk", "hard drive") || (Has(normalizedText, "7200 rpm") || Has(normalizedText, "5400 rpm")))
            return ("Storage", "HDD");
        if (Has(normalizedText, "sequential read", "sequential write", "tbw", "nand"))
            return ("Storage", null);

        // Motherboard
        if (Has(normalizedText, "motherboard", "placa de baza", "chipset", "vrm", "pcie x16", "dimm slot"))
            return ("Motherboard", null);

        // PSU
        if (Has(normalizedText, "power supply", "sursa de alimentare", "80plus", "80 plus", "modular", "atx12v", "watt"))
            return ("PSU", null);

        // Cooler — liquid vs air
        if (Has(normalizedText, "aio", "liquid cooling", "all-in-one cooler", "240mm", "280mm", "360mm", "water block", "racire lichid"))
            return ("Cooler", "Lichid");
        if (Has(normalizedText, "heatsink", "heat pipe", "air cooler", "tower cooler", "racire aer"))
            return ("Cooler", "Aer");
        if (Has(normalizedText, "cooler", "racire", "heat pipe"))
            return ("Cooler", null);

        // Case
        if (Has(normalizedText, "carcasa", "chassis", "mid tower", "full tower", "mini tower", "tempered glass", "pc case"))
            return ("Case", null);

        return (null, null);
    }

    private static string? DetectProductName(string rawText)
    {
        var keyName = TryExtractNameFromLabeledLine(rawText);
        if (!string.IsNullOrWhiteSpace(keyName))
            return SanitizeSuggestedProductName(keyName);

        var lines = rawText
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanLine)
            .Where(l => l.Length > 3)
            .ToList();

        foreach (var line in lines.Take(20))
        {
            if (line.Contains(':') || line.Contains("www", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.Length is < 6 or > 90)
                continue;

            if (Regex.IsMatch(line, @"\d{4,}"))
                continue;

            if (ContainsAny(line, "specification", "datasheet", "manual", "rev", "version", "copyright"))
                continue;

            var sanitized = SanitizeSuggestedProductName(line);
            if (!string.IsNullOrWhiteSpace(sanitized))
                return sanitized;
        }

        return null;
    }

    private static string? TryExtractNameFromLabeledLine(string rawText)
    {
        foreach (var (keyCandidate, valueCandidate) in ExtractKeyValueCandidates(rawText))
        {
            var normalizedKey = NormalizeForMatching(keyCandidate);
            if (normalizedKey is "name" or "product name" or "model" or "model name")
            {
                var cleanedValue = NormalizeSpecValue(valueCandidate);
                var sanitized = SanitizeSuggestedProductName(cleanedValue);
                if (!string.IsNullOrWhiteSpace(sanitized))
                    return sanitized;
            }
        }

        return null;
    }

    private static IEnumerable<(string Key, string Value)> ExtractKeyValueCandidates(string rawText)
    {
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First pass: classic per-line extraction when line breaks are meaningful.
        var lines = rawText
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanLine)
            .Where(l => l.Length > 2);

        foreach (var line in lines)
        {
            if (!TryExtractPair(line, out var key, out var value))
                continue;

            var signature = $"{NormalizeForMatching(key)}::{NormalizeForMatching(value)}";
            if (!emitted.Add(signature))
                continue;

            yield return (key, value);
        }

        // Second pass: flattened text extraction for PDFs where PdfPig returns one long line.
        var flattened = Regex.Replace(rawText, @"\s+", " ").Trim();
        if (flattened.Length == 0)
            yield break;

        var matches = Regex.Matches(
            flattened,
            @"(?<key>[A-Za-z][A-Za-z0-9\/\-\+\.\(\) ]{1,40})\s*:\s*(?<value>.*?)(?=(?:\s+[A-Za-z][A-Za-z0-9\/\-\+\.\(\) ]{1,40}\s*:)|$)",
            RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            if (!match.Success)
                continue;

            var key = CleanLine(match.Groups["key"].Value);
            var value = CleanLine(match.Groups["value"].Value);

            if (key.Length is < 2 or > 80 || value.Length < 2)
                continue;

            var signature = $"{NormalizeForMatching(key)}::{NormalizeForMatching(value)}";
            if (!emitted.Add(signature))
                continue;

            yield return (key, value);
        }
    }

    private static string? SanitizeSuggestedProductName(string input)
    {
        var name = CleanLine(input);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        name = Regex.Replace(
            name,
            @"^(cpu|gpu|ram|storage|motherboard|psu|cooler|case|procesor|placa\s+video|memorie\s+ram|sursa(?:\s+de\s+alimentare)?)\s*[:\-]\s*",
            string.Empty,
            RegexOptions.IgnoreCase);

        if (name.Contains(':'))
            return null;

        if (Regex.IsMatch(name, @"\b\d{2,5}\s?(mhz|ghz|w|rpm|gb|tb|mm|v)\b", RegexOptions.IgnoreCase))
            return null;

        if (name.Length is < 3 or > 120)
            return null;

        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 12)
            return null;

        return name;
    }

    private static bool Has(string text, params string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeForMatching(string input)
        => Regex.Replace(input.ToLowerInvariant(), @"\s+", " ").Trim();

    private static string CleanLine(string line)
        => Regex.Replace(line.Replace('\t', ' '), @"\s+", " ").Trim();

    private static bool TryExtractPair(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var separatorIndex = line.IndexOf(':');
        if (separatorIndex < 1)
            separatorIndex = line.IndexOf(" - ", StringComparison.Ordinal);
        if (separatorIndex < 1)
            separatorIndex = line.IndexOf(" – ", StringComparison.Ordinal);

        if (separatorIndex < 1 || separatorIndex >= line.Length - 1)
            return false;

        key = line[..separatorIndex].Trim();
        value = line[(separatorIndex + (line[separatorIndex] == ':' ? 1 : 3))..].Trim();

        if (key.Length is < 2 or > 80 || value.Length < 2)
            return false;

        if (Regex.IsMatch(key, @"^\d+([\.,]\d+)?$"))
            return false;

        return true;
    }

    private static string? NormalizeSpecKey(string key)
    {
        var normalized = NormalizeForMatching(key);

        return normalized switch
        {
            "name" or "product name" or "model" or "model name" or "product" or "sku" => null,
            "cpu" or "processor" or "procesor" => "processor",
            "socket" => "socket",
            "cores" or "nuclee" => "cores",
            "threads" or "fire" => "threads",
            "base clock" or "base frequency" => "base_clock",
            "boost clock" or "turbo frequency" => "boost_clock",
            "cache" or "l3 cache" => "cache",
            "gpu" or "graphics card" or "placa video" => "gpu",
            "vram" or "memory" => "vram",
            "memory type" => "memory_type",
            "memory bus" or "bus width" => "bus_width",
            "ram" or "capacity" => "capacity",
            "ram support" => "ram_support",
            "speed" or "memory speed" => "speed",
            "latency" or "cas latency" => "latency",
            "type" => "type",
            "interface" => "interface",
            "read speed" or "sequential read" => "read_speed",
            "write speed" or "sequential write" => "write_speed",
            "tbw" => "tbw",
            "tdp" => "tdp",
            "wattage" or "power" or "putere" => "wattage",
            "efficiency" or "eficienta" => "efficiency",
            "modular" => "modular",
            "cooler type" or "cooling type" => "cooling_type",
            "fan size" or "fan" => "fan_size",
            _ when normalized.Length <= 40 => normalized.Replace(' ', '_'),
            _ => null
        };
    }

    private static void RemoveNonSpecificationKeys(Dictionary<string, string> specs)
    {
        specs.Remove("name");
        specs.Remove("product_name");
        specs.Remove("model");
        specs.Remove("model_name");
        specs.Remove("product");
        specs.Remove("sku");
        specs.Remove("category");
    }

    private static string NormalizeSpecValue(string value)
    {
        var cleaned = CleanLine(value);
        cleaned = Regex.Replace(cleaned, @"\s*(gb|tb|mb|mhz|ghz|w|mm|rpm|vram|gddr\d+)\b", " $1", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return cleaned;
    }

    private static void FillRegexBasedMissingSpecs(string rawText, Dictionary<string, string> specs)
    {
        AddIfMissing(specs, "socket", MatchGroup(rawText, @"\b(?:socket)\s*(AM\d|LGA\s*\d{3,4})\b", 1));
        AddIfMissing(specs, "capacity", MatchGroup(rawText, @"\b(\d{1,3}\s?(?:GB|TB))\b", 1));
        AddIfMissing(specs, "speed", MatchGroup(rawText, @"\b(\d{3,5}\s?(?:MHz|MT/s))\b", 1));
        AddIfMissing(specs, "wattage", MatchGroup(rawText, @"\b(\d{2,4}\s?W)\b", 1));
        AddIfMissing(specs, "vram", MatchGroup(rawText, @"\b(\d{1,2}\s?GB\s?GDDR\dX?)\b", 1));
    }

    private static string? MatchGroup(string text, string pattern, int group)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? NormalizeSpecValue(match.Groups[group].Value) : null;
    }

    private static void AddIfMissing(Dictionary<string, string> specs, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || specs.ContainsKey(key))
            return;

        specs[key] = value;
    }

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
}
