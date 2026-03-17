using System.ComponentModel.DataAnnotations;

namespace Recomandare_PC.Models;

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Subcategory> Subcategories { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}
