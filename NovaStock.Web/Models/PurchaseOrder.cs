using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaStock.Web.Models;

/// <summary>
/// Satın Alma Siparişi başlığı. Tedarikçiden mal alımını temsil eder.
/// </summary>
public class PurchaseOrder : BaseEntity
{
    [Required, MaxLength(30)]
    public string PurchaseNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public int? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    public DateTime? OrderedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public string? CreatedByUserId { get; set; }

    // Navigation
    public ICollection<PurchaseOrderItem> Items { get; set; } = [];
}

public enum PurchaseOrderStatus
{
    Draft     = 0,
    Ordered   = 1,
    Received  = 2,
    Cancelled = 3
}
