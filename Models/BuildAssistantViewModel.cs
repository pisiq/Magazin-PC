using Recomandare_PC.DTOs;

namespace Recomandare_PC.Models;

public class BuildAssistantViewModel
{
    public string? Cpu { get; set; }
    public string? Gpu { get; set; }
    public string? Ram { get; set; }
    public string? Motherboard { get; set; }
    public string? Psu { get; set; }
    public string? Storage { get; set; }
    public string? CoolingPreference { get; set; }

    public decimal? Budget { get; set; }

    public RecommendationResponse? Recommendation { get; set; }
    public string? ErrorMessage { get; set; }

    public BuildAssistantRequestDto ToRequestDto() => new()
    {
        Cpu = Cpu,
        Gpu = Gpu,
        Ram = Ram,
        Motherboard = Motherboard,
        Psu = Psu,
        Storage = Storage,
        CoolingPreference = CoolingPreference,
        Budget = Budget,
    };
}

