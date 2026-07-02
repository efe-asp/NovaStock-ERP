namespace NovaStock.Web.Models;

/// <summary>
/// Ürün-Depo ara tablosu (Many-to-Many). Her depodan ayrı stok miktarı tutulur.
/// </summary>
public class ProductWarehouse : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    /// <summary>Bu depodaki ürün stok miktarı.</summary>
    public int Quantity { get; set; }

    public string? Location { get; set; } // Raf/Bölüm
}
