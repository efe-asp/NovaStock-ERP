using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaStock.Web.Models;

/// <summary>
/// Satın Alma Siparişi kalemi. Hangi üründen kaç adet alındığını tutar.
/// </summary>
public class PurchaseOrderItem : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    /// <summary>Birim alış maliyeti.</summary>
    [Required, Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    [NotMapped]
    public decimal LineTotal => Quantity * UnitCost;
}
