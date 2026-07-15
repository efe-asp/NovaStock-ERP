using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaStock.Web.Models;

/// <summary>
/// ASP.NET Core Identity kullanıcısı.
/// Bayi kademeleri: Bronze / Silver / Gold
/// Roller: Admin / Dealer / DealerPurchase / DealerFinance
/// Sub-user mimarisi: ParentDealerId ile ana bayiye bağlanır.
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

    // ─── Sub-User (Multi-Tenant B2B) ──────────────────────────────────────────
    /// <summary>
    /// Alt kullanıcı ise ana bayinin ID'si. Null ise ana kullanıcı.
    /// Örn: Patron (Ana Dealer) → Satın Alma Personeli (DealerPurchase sub-user)
    /// </summary>
    public string? ParentDealerId { get; set; }

    [ForeignKey(nameof(ParentDealerId))]
    public ApplicationUser? ParentDealer { get; set; }

    /// <summary>
    /// Admin tarafından manuel olarak belirlenen kredi limiti.
    /// Null ise Tier'a göre otomatik limit hesaplanır.
    /// Gold: 500.000 / Silver: 200.000 / Bronze: 50.000 ₺
    /// </summary>
    public decimal? CreditLimitOverride { get; set; }

    // ─── Navigation ───────────────────────────────────────────────────────────
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = [];
    public ICollection<SupportTicket> SupportTickets { get; set; } = [];
    public ICollection<ApplicationUser> SubUsers { get; set; } = [];
}

public enum DealerTier
{
    Bronze = 0,
    Silver = 1,
    Gold = 2
}
