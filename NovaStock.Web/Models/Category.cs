using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaStock.Web.Models;

/// <summary>
/// Ürün kategorisi. Bayiler kategori bazlı filtreleme yapabilir.
/// </summary>
public class Category : BaseEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public string? IconClass { get; set; } = "fa-box";

    // Navigation
    public ICollection<Product> Products { get; set; } = [];
}
