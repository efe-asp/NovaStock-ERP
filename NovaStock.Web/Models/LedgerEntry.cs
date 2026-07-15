using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaStock.Web.Models;

/// <summary>
/// Cari hesap hareketi (Muhasebe Defteri Kaydı).
/// Borç (debit) ve alacak (credit) kayıtlarını tutar.
/// Her sipariş tesliminde otomatik Borç kaydı oluşur;
/// her ödeme işleminde Alacak kaydı oluşur.
/// </summary>
public class LedgerEntry : BaseEntity
{
    /// <summary>Kaydın ait olduğu bayi kullanıcı ID'si.</summary>
    [Required]
    public string DealerId { get; set; } = string.Empty;
    public ApplicationUser Dealer { get; set; } = null!;

    /// <summary>İşlem türü: Invoice, Payment, Refund, BankTransfer, VirtualPos</summary>
    public LedgerTransactionType TransactionType { get; set; }

    /// <summary>Evrak numarası. Örn: FAT-2026-001, TX-99821, HVL-0042</summary>
    [MaxLength(50)]
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>Borç (Debit) tutarı – bayinin borcunu artırır.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Debit { get; set; } = 0m;

    /// <summary>Alacak (Credit) tutarı – bayinin borcunu azaltır.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Credit { get; set; } = 0m;

    /// <summary>İşlem sonrası güncel kalan bakiye.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal RunningBalance { get; set; } = 0m;

    /// <summary>İşlem açıklaması.</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Vade tarihi (faturalar için). Geçmişte kaldıysa vadesi geçmiş borç sayılır.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>İlgili sipariş (fatura kaydı için).</summary>
    public int? RelatedOrderId { get; set; }
    public Order? RelatedOrder { get; set; }

    /// <summary>İşlem tarihi (ödeme / fatura kesilme tarihi).</summary>
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}

public enum LedgerTransactionType
{
    Invoice      = 0,  // Fatura (Borç)
    Payment      = 1,  // Ödeme / Tahsilat
    Refund       = 2,  // İade
    BankTransfer = 3,  // Havale / EFT
    VirtualPos   = 4,  // Web Sanal POS Tahsilatı
    Adjustment   = 5   // Manuel Düzeltme (Admin)
}
