using Recomandare_PC.DTOs;

namespace Recomandare_PC.Models;

public sealed class SpecificationSearchViewModel
{
    public string Query { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public string ScoreSort { get; init; } = "desc";
    public IReadOnlyList<Category> Categories { get; init; } = [];
    public IReadOnlyList<(ProductListDto Product, float Score)> Results { get; init; } = [];
}

