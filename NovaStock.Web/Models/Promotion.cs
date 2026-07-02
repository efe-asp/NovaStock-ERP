using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaStock.Web.Models;

/// <summary>
/// Kampanya / İndirim Kuralı. Promotion Engine tarafından işlenir.
/// </summary>
public class Promotion : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public PromotionType Type { get; set; }

    /// <summary>İndirim yüzdesi veya sabit tutarı.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountValue { get; set; }

    /// <summary>Minimum sepet tutarı koşulu.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinimumCartTotal { get; set; }

    /// <summary>Minimum ürün adedi koşulu.</summary>
    public int? MinimumQuantity { get; set; }

    /// <summary>Geçerli olduğu kategori (opsiyonel).</summary>
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? ValidUntil { get; set; }
}

public enum PromotionType
{
    PercentageDiscount = 0,
    FixedDiscount = 1,
    FreeShipping = 2,
    BuyXGetY = 3       // 3 al 2 öde mantığı
}
