using System.ComponentModel.DataAnnotations;
using NovaStock.Web.Models;

namespace NovaStock.Web.ViewModels;

// ─── Destek Talepleri ─────────────────────────────────────────────────────────
public class SupportTicketListItem
{
    public int            Id              { get; set; }
    public string         TicketCode      => $"#DST-{Id:D4}";
    public string         Subject         { get; set; } = string.Empty;
    public TicketCategory Category        { get; set; }
    public TicketPriority Priority        { get; set; }
    public TicketStatus   Status          { get; set; }
    public string?        RelatedOrderNumber { get; set; }
    public string?        AssignedToName  { get; set; }
    public DateTime       CreatedAt       { get; set; }
    public DateTime       LastActivityAt  { get; set; }
    public int            UnreadCount     { get; set; }

    public string CategoryLabel => Category switch
    {
        TicketCategory.Finance  => "Finans / Fatura",
        TicketCategory.Logistics => "Lojistik / Kargo",
        TicketCategory.RMA      => "Arızalı Ürün / RMA",
        _                       => "Diğer"
    };

    public string PriorityLabel => Priority switch
    {
        TicketPriority.High   => "Yüksek",
        TicketPriority.Medium => "Orta",
        _                     => "Düşük"
    };

    public string PriorityBadgeClass => Priority switch
    {
        TicketPriority.High   => "badge-red",
        TicketPriority.Medium => "badge-yellow",
        _                     => "badge-gray"
    };

    public string StatusLabel => Status switch
    {
        TicketStatus.Answered => "Yanıtlandı",
        TicketStatus.Resolved => "Kapatıldı",
        _                     => "Beklemede"
    };

    public string StatusBadgeClass => Status switch
    {
        TicketStatus.Answered => "badge-cyan",
        TicketStatus.Resolved => "badge-gray",
        _                     => "badge-yellow"
    };
}

public class TicketMessageViewModel
{
    public int      Id          { get; set; }
    public string   SenderName  { get; set; } = string.Empty;
    public string   Body        { get; set; } = string.Empty;
    public bool     IsFromAdmin { get; set; }
    public DateTime CreatedAt   { get; set; }
    public List<AttachmentItem> Attachments { get; set; } = [];
}

public class AttachmentItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long   FileSize { get; set; }
    public string FileSizeLabel => FileSize > 1024 * 1024
        ? $"{FileSize / 1024 / 1024:N1} MB"
        : $"{FileSize / 1024:N0} KB";
    public bool IsImage => FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        || FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                        || FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        || FileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
                        || FileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
}

public class SupportTicketDetailViewModel
{
    public int            Id              { get; set; }
    public string         TicketCode      => $"#DST-{Id:D4}";
    public string         Subject         { get; set; } = string.Empty;
    public TicketCategory Category        { get; set; }
    public TicketPriority Priority        { get; set; }
    public TicketStatus   Status          { get; set; }
    public string?        RelatedOrderNumber { get; set; }
    public int?           RelatedOrderId  { get; set; }
    public string?        AssignedToId    { get; set; }
    public string?        AssignedToName  { get; set; }
    public string         DealerName      { get; set; } = string.Empty;
    public string         DealerCompany   { get; set; } = string.Empty;
    public DateTime       CreatedAt       { get; set; }
    public DateTime       LastActivityAt  { get; set; }
    public bool           IsAdmin         { get; set; }

    /// <summary>Admin kullanıcı listesi – temsilci atama dropdown'ı için.</summary>
    public List<AdminSelectItem> AdminUsers { get; set; } = [];

    public List<TicketMessageViewModel> Messages { get; set; } = [];

    public string CategoryLabel => Category switch
    {
        TicketCategory.Finance  => "Finans / Fatura",
        TicketCategory.Logistics => "Lojistik / Kargo",
        TicketCategory.RMA      => "Arızalı Ürün / RMA",
        _                       => "Diğer"
    };
    public string CategoryIcon => Category switch
    {
        TicketCategory.Finance  => "fa-file-invoice-dollar",
        TicketCategory.Logistics => "fa-truck",
        TicketCategory.RMA      => "fa-rotate-left",
        _                       => "fa-question-circle"
    };
    public string PriorityLabel => Priority switch
    {
        TicketPriority.High   => "Yüksek",
        TicketPriority.Medium => "Orta",
        _                     => "Düşük"
    };
    public string PriorityBadgeClass => Priority switch
    {
        TicketPriority.High   => "badge-red",
        TicketPriority.Medium => "badge-yellow",
        _                     => "badge-gray"
    };
    public string StatusLabel => Status switch
    {
        TicketStatus.Answered => "Yanıtlandı",
        TicketStatus.Resolved => "Kapatıldı",
        _                     => "Beklemede"
    };
    public string StatusBadgeClass => Status switch
    {
        TicketStatus.Answered => "badge-cyan",
        TicketStatus.Resolved => "badge-gray",
        _                     => "badge-yellow"
    };
}

public class CreateTicketViewModel
{
    [Required(ErrorMessage = "Konu alanı zorunludur.")]
    [MaxLength(200, ErrorMessage = "Konu en fazla 200 karakter olabilir.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kategori seçiniz.")]
    public TicketCategory Category { get; set; }

    [Required(ErrorMessage = "Önem derecesi seçiniz.")]
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    [Required(ErrorMessage = "Mesaj içeriği zorunludur.")]
    [MaxLength(4000, ErrorMessage = "Mesaj en fazla 4000 karakter olabilir.")]
    public string Message { get; set; } = string.Empty;

    public string? RelatedOrderNumber { get; set; }

    public IFormFile? Attachment { get; set; }
}

public class ReplyViewModel
{
    [Required]
    public int TicketId { get; set; }

    [Required(ErrorMessage = "Yanıt mesajı zorunludur.")]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    public IFormFile? Attachment { get; set; }

    public bool CloseTicket { get; set; } = false;
}

/// <summary>Admin dropdown'ı için basit ID+Ad modeli.</summary>
public class AdminSelectItem
{
    public string Id       { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

// ─── Ledger / Statement ───────────────────────────────────────────────────────
public class LedgerEntryViewModel
{
    public int    Id              { get; set; }
    public string DocumentNumber  { get; set; } = string.Empty;
    public decimal Debit          { get; set; }
    public decimal Credit         { get; set; }
    public decimal RunningBalance { get; set; }
    public string? Description    { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? DueDate      { get; set; }
    public int?   RelatedOrderId  { get; set; }

    public string TransactionTypeLabel { get; set; } = string.Empty;
    public string TransactionTypeIcon  { get; set; } = string.Empty;
    public string TransactionTypeBadge { get; set; } = string.Empty;

    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTime.UtcNow && Debit > 0;
}

public class StatementViewModel
{
    public string DealerName    { get; set; } = string.Empty;
    public string CompanyName   { get; set; } = string.Empty;
    public string Tier          { get; set; } = string.Empty;
    public decimal TotalDebt    { get; set; }
    public decimal TotalCredit  { get; set; }
    public decimal NetBalance   { get; set; }   // Pozitif = borç, negatif = alacak
    public decimal CreditLimit  { get; set; }
    public decimal OverdueDebt  { get; set; }
    public decimal UsedLimitPct { get; set; }

    public List<LedgerEntryViewModel> Entries { get; set; } = [];
}

// ─── Sub-User Yönetimi ────────────────────────────────────────────────────────
public class SubUserViewModel
{
    public string Id          { get; set; } = string.Empty;
    public string FullName    { get; set; } = string.Empty;
    public string Email       { get; set; } = string.Empty;
    public string Role        { get; set; } = string.Empty;
    public string RoleLabel   { get; set; } = string.Empty;
    public bool   IsActive    { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSubUserViewModel
{
    [Required(ErrorMessage = "Ad Soyad zorunludur.")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rol seçiniz.")]
    public string Role { get; set; } = "DealerPurchase"; // DealerPurchase veya DealerFinance
}
