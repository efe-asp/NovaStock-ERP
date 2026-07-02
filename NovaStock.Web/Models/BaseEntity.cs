namespace NovaStock.Web.Models;

/// <summary>
/// Tüm entity sınıflarının miras aldığı temel sınıf.
/// Kurumsal standartlarda Soft Delete altyapısını sağlar.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True olduğunda kayıt "silinmiş" sayılır; veritabanından fiziksel olarak kaldırılmaz.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
}
