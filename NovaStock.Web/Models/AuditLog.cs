using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaStock.Web.Models;

/// <summary>
/// Denetim Günlüğü. "Kim ne zaman ne değiştirdi?" kaydı tutar.
/// EF Core SaveChanges override ile otomatik doldurulur.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Değişiklik yapan kullanıcı adı.</summary>
    [MaxLength(256)]
    public string? UserName { get; set; }

    /// <summary>Etkilenen tablo adı.</summary>
    [Required, MaxLength(100)]
    public string TableName { get; set; } = string.Empty;

    /// <summary>İşlem türü: Insert / Update / Delete</summary>
    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Birincil anahtar değeri.</summary>
    public string? RecordId { get; set; }

    /// <summary>Değişiklik öncesi JSON verisi.</summary>
    public string? OldValues { get; set; }

    /// <summary>Değişiklik sonrası JSON verisi.</summary>
    public string? NewValues { get; set; }

    /// <summary>İnsan okunabilir açıklama.</summary>
    public string? Description { get; set; }
}
