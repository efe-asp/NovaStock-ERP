using System.ComponentModel.DataAnnotations;

namespace NovaStock.Web.Models;

/// <summary>
/// B2B Destek Talebi (Support Ticket / RMA).
/// Bayilerin finans, lojistik, ürün arızası ve genel konularda
/// admin ile yazışmasını sağlayan bilet sistemi.
/// </summary>
public class SupportTicket : BaseEntity
{
    /// <summary>Talebi açan bayi ID'si.</summary>
    [Required]
    public string DealerId { get; set; } = string.Empty;
    public ApplicationUser Dealer { get; set; } = null!;

    /// <summary>Talep başlığı.</summary>
    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Talep kategorisi.</summary>
    public TicketCategory Category { get; set; } = TicketCategory.Other;

    /// <summary>Önem derecesi.</summary>
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    /// <summary>Talep durumu.</summary>
    public TicketStatus Status { get; set; } = TicketStatus.Open;

    /// <summary>İlgili sipariş numarası (varsa, lojistik ve RMA talepleri için).</summary>
    [MaxLength(30)]
    public string? RelatedOrderNumber { get; set; }

    /// <summary>İlgili sipariş (FK).</summary>
    public int? RelatedOrderId { get; set; }
    public Order? RelatedOrder { get; set; }

    /// <summary>Atanmış müşteri temsilcisi (admin kullanıcı ID).</summary>
    public string? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    /// <summary>Son güncelleme zamanı (mesaj geldiğinde güncellenir).</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>Kapatılma tarihi.</summary>
    public DateTime? ClosedAt { get; set; }

    // Navigation
    public ICollection<TicketMessage> Messages { get; set; } = [];
}

public enum TicketCategory
{
    Finance  = 0,  // Finans / Fatura
    Logistics = 1, // Lojistik / Kargo
    RMA      = 2,  // Arızalı Ürün / İade (RMA)
    Other    = 3   // Diğer
}

public enum TicketPriority
{
    Low    = 0,
    Medium = 1,
    High   = 2
}

public enum TicketStatus
{
    Open     = 0,  // Açık / Beklemede
    Answered = 1,  // Yanıtlandı
    Resolved = 2   // Çözüldü / Kapatıldı
}
