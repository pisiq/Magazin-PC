using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Recomandare_PC.Models;

public class Subcategory
{
    public int Id { get; set; }

    public int CategoryId { get; set; }
    [ValidateNever]
    public Category Category { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [ValidateNever]
    public ICollection<Product> Products { get; set; } = [];
}
