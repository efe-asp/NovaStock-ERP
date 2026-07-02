using System.ComponentModel.DataAnnotations;

namespace NovaStock.Web.Models;

/// <summary>
/// Depo (Warehouse). Çoklu şehir/lokasyon stok takibi.
/// </summary>
public class Warehouse : BaseEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string City { get; set; } = string.Empty;

    public string? Address { get; set; }
    public string? ManagerName { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<ProductWarehouse> ProductWarehouses { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
}
