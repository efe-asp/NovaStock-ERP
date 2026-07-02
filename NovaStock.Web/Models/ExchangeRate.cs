using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaStock.Web.Models;

/// <summary>
/// Döviz Kuru. Background service tarafından her sabah güncellenir.
/// </summary>
public class ExchangeRate : BaseEntity
{
    [Required, MaxLength(10)]
    public string CurrencyCode { get; set; } = string.Empty; // USD, EUR vb.

    [Column(TypeName = "decimal(18,4)")]
    public decimal Rate { get; set; } // TL karşılığı

    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public string? Source { get; set; } = "TCMB";
}
