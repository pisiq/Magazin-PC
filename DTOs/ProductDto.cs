namespace Recomandare_PC.DTOs;

public record ProductDto(
    int Id,
    string Name,
    decimal Price,
    int StockQuantity,
    string CategoryName,
    string? SubcategoryName,
    string? Specifications
);

public record ProductListDto(
    int Id,
    string Name,
    decimal Price,
    int StockQuantity,
    string CategoryName,
    string? SubcategoryName
);
