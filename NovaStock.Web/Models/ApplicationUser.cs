using Microsoft.AspNetCore.Identity;

namespace NovaStock.Web.Models;

/// <summary>
/// ASP.NET Core Identity kullanıcısı.
/// Bayi kademeleri: Bronze / Silver / Gold
/// Roller: Admin / Dealer
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }

    /// <summary>Bayi kademesi: Bronze, Silver, Gold</summary>
    public DealerTier Tier { get; set; } = DealerTier.Bronze;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Order> Orders { get; set; } = [];
}

public enum DealerTier
{
    Bronze = 0,
    Silver = 1,
    Gold = 2
}
