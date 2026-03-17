using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Recomandare_PC.Models;

public class Product
{
    public int Id { get; set; }

    public int CategoryId { get; set; }
    [ValidateNever]
    public Category Category { get; set; } = null!;

    public int? SubcategoryId { get; set; }
    [ValidateNever]
    public Subcategory? Subcategory { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    /// <summary>
    /// Serialized JSON with technical specifications extracted from PDF.
    /// </summary>
    public string? Specifications { get; set; }

    /// <summary>
    /// Relative path inside wwwroot/pdfs/ — e.g., "pdfs/product-123.pdf"
    /// </summary>
    [MaxLength(500)]
    public string? PdfPath { get; set; }
}
