using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaStock.Web.Models;

/// <summary>
/// Sistem bildirimlerini temsil eder.
/// </summary>
public class Notification : BaseEntity
{
    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Bildirim türü. Örn: "order", "stock", "system"
    /// </summary>
    [Required, StringLength(50)]
    public string Type { get; set; } = "system";

    /// <summary>
    /// Bildirimin UI'da gösterilecek ikonu. Örn: "fa-cart-check"
    /// </summary>
    [StringLength(50)]
    public string? IconClass { get; set; }

    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Eğer bildirim belirli bir kullanıcıya aitse UserId doldurulur.
    /// Null ise tüm Adminlere (veya genel gruba) atılmış demektir.
    /// </summary>
    [StringLength(450)]
    public string? UserId { get; set; }

    [ForeignKey("UserId")]
    public ApplicationUser? User { get; set; }
}
