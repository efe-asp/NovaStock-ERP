using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaStock.Web.Models;

/// <summary>
/// Ürün modeli. Çoklu depo stok takibi ve kritik stok alarmını destekler.
/// </summary>
public class Product : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Stok Tutma Birimi – benzersiz ürün kodu.</summary>
    [Required, MaxLength(50)]
    public string SKU { get; set; } = string.Empty;

    /// <summary>Barkod (EAN, UPC, veya özel okutma kodu).</summary>
    [MaxLength(100)]
    public string? Barcode { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, Column(TypeName = "decimal(18,2)")]
    public decimal BasePrice { get; set; }

    /// <summary>Toplam stok (tüm depoların toplamı).</summary>
    public int StockCount { get; set; }

    /// <summary>Kritik stok eşiği – altına indiğinde kırmızı alarm tetiklenir.</summary>
    public int CriticalStockLevel { get; set; } = 5;

    /// <summary>Ürün USD fiyatı (döviz motoru tarafından güncellenir).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? PriceUSD { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    // Relations
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = [];
    public ICollection<ProductWarehouse> ProductWarehouses { get; set; } = [];

    // Computed
    [NotMapped]
    public bool IsCriticalStock => StockCount <= CriticalStockLevel;
}
