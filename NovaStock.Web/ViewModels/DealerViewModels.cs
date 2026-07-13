using System.ComponentModel.DataAnnotations;
using NovaStock.Web.Models;

namespace NovaStock.Web.ViewModels;

// ─── Bayi Listesi ────────────────────────────────────────────────────────────
public class DealerListItem
{
    public string  Id           { get; set; } = string.Empty;
    public string  FullName     { get; set; } = string.Empty;
    public string  CompanyName  { get; set; } = string.Empty;
    public string  Email        { get; set; } = string.Empty;
    public string? Phone        { get; set; }
    public DealerTier Tier      { get; set; }
    public bool    IsActive     { get; set; }
    public int     OrderCount   { get; set; }
    public decimal TotalSpent   { get; set; }
    public DateTime CreatedAt   { get; set; }
}

// ─── Bayi Detay ──────────────────────────────────────────────────────────────
public class DealerDetailViewModel
{
    public string  Id           { get; set; } = string.Empty;
    public string  FullName     { get; set; } = string.Empty;
    public string  CompanyName  { get; set; } = string.Empty;
    public string  Email        { get; set; } = string.Empty;
    public string? Phone        { get; set; }
    public string? Address      { get; set; }
    public string? TaxNumber    { get; set; }
    public DealerTier Tier      { get; set; }
    public bool    IsActive     { get; set; }
    public DateTime CreatedAt   { get; set; }

    public int     OrderCount   { get; set; }
    public decimal TotalSpent   { get; set; }
    public decimal PendingAmount { get; set; }

    public List<DealerOrderItem> RecentOrders { get; set; } = [];
}

public class DealerOrderItem
{
    public int     Id          { get; set; }
    public string  OrderNumber { get; set; } = string.Empty;
    public decimal Total       { get; set; }
    public string  Status      { get; set; } = string.Empty;
    public DateTime CreatedAt  { get; set; }
}

// ─── Bayi Düzenleme ──────────────────────────────────────────────────────────
public class DealerEditViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kademe seçiniz.")]
    public DealerTier Tier { get; set; }

    public bool IsActive { get; set; }
}

// ─── Cari Hesap Ekstresi ─────────────────────────────────────────────────────
public class DealerStatementItem
{
    public int      Id          { get; set; }
    public string   OrderNumber { get; set; } = string.Empty;
    public decimal  Total       { get; set; }
    public string   Status      { get; set; } = string.Empty;
    public DateTime CreatedAt   { get; set; }
}
