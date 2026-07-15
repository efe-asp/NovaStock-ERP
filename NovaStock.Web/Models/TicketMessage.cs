using System.ComponentModel.DataAnnotations;

namespace NovaStock.Web.Models;

/// <summary>
/// Destek talebi mesajı. Her talep altında birden fazla mesaj olabilir.
/// Bayi ve admin iki taraflı yazışabilir. Dosya ekleri JSON olarak saklanır.
/// </summary>
public class TicketMessage : BaseEntity
{
    public int SupportTicketId { get; set; }
    public SupportTicket SupportTicket { get; set; } = null!;

    /// <summary>Mesajı gönderen kullanıcı ID.</summary>
    [Required]
    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;

    /// <summary>Mesaj içeriği.</summary>
    [Required, MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    /// <summary>Gönderenin rolü (Dealer / Admin) – timeline renklendirmesi için.</summary>
    public bool IsFromAdmin { get; set; } = false;

    /// <summary>
    /// Ekli dosyalar – JSON array olarak saklanır.
    /// Örn: [{"FileName":"kargo_foto.jpg","FilePath":"/uploads/tickets/xxx.jpg","FileSize":102400}]
    /// </summary>
    public string? AttachmentsJson { get; set; }

    /// <summary>Okundu mu? (Bildirim sistemi için)</summary>
    public bool IsRead { get; set; } = false;
}
