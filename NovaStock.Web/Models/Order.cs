using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaStock.Web.Models;

/// <summary>
/// Sipariş başlığı. Her sipariş bir bayiye aittir.
/// </summary>
public class Order : BaseEntity
{
    [Required, MaxLength(30)]
    public string OrderNumber { get; set; } = string.Empty;

    public string DealerId { get; set; } = string.Empty;
    public ApplicationUser Dealer { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    public string? Notes { get; set; }
    public string? PdfPath { get; set; }

    // Fatura adresi
    public string? BillingAddress { get; set; }

    public int? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    // Navigation
    public ICollection<OrderItem> Items { get; set; } = [];
}

public enum OrderStatus
{
    Pending = 0,
    Approved = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4
}
