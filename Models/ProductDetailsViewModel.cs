using Recomandare_PC.DTOs;

namespace Recomandare_PC.Models;

public class ProductDetailsViewModel
{
    public ProductDto Product { get; set; } = null!;
    public IReadOnlyList<KeyValuePair<string, string>> Specifications { get; set; } = [];
    public IReadOnlyList<(ProductListDto Product, double Score)> SimilarProducts { get; set; } = [];
}

