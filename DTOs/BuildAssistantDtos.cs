namespace Recomandare_PC.DTOs;

/// <summary>
/// Request contract used by UI and mobile/LAN clients for partial PC builds.
/// </summary>
public class BuildAssistantRequestDto
{
    public string? Cpu { get; set; }
    public string? Gpu { get; set; }
    public string? Ram { get; set; }
    public string? Motherboard { get; set; }
    public string? Psu { get; set; }
    public string? Storage { get; set; }
    public string? CoolingPreference { get; set; }

    public decimal? Budget { get; set; }

    /// <summary>
    /// Optional source identifier for future phone-over-WiFi payloads.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Flexible category map for future app versions that may send extra components.
    /// </summary>
    public Dictionary<string, string>? Components { get; set; }
}

public static class BuildAssistantRequestMapper
{
    public static RecommendationRequest ToRecommendationRequest(this BuildAssistantRequestDto request)
    {
        var existing = new List<ExistingComponent>();

        AddIfProvided(existing, "CPU", request.Cpu);
        AddIfProvided(existing, "GPU", request.Gpu);
        AddIfProvided(existing, "RAM", request.Ram);
        AddIfProvided(existing, "Motherboard", request.Motherboard);
        AddIfProvided(existing, "PSU", request.Psu);
        AddIfProvided(existing, "Storage", request.Storage);
        AddIfProvided(existing, "Cooler", NormalizeCoolingPreference(request.CoolingPreference));

        if (request.Components is not null)
        {
            foreach (var pair in request.Components)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    continue;

                if (existing.Any(e => string.Equals(e.Category, pair.Key, StringComparison.OrdinalIgnoreCase)))
                    continue;

                existing.Add(new ExistingComponent(pair.Key.Trim(), pair.Value.Trim()));
            }
        }

        return new RecommendationRequest(existing, request.Budget, NormalizeCoolingPreference(request.CoolingPreference));
    }

    private static string? NormalizeCoolingPreference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch
        {
            "air" => "Air",
            "liquid" => "Liquid",
            _ => null
        };
    }

    private static void AddIfProvided(List<ExistingComponent> existing, string category, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        existing.Add(new ExistingComponent(category, value.Trim()));
    }
}


