namespace NovaStock.Web.ViewModels;

/// <summary>
/// Dashboard ana sayfası için veri modeli.
/// Controller tarafından hesaplanır, View'e gönderilir.
/// </summary>
public class DashboardViewModel
{
    // ─── Özet Kartlar ───────────────────────────────────────────────────────────
    public int  TotalProducts        { get; set; }
    public int  CriticalStockCount   { get; set; }
    public int  TotalCategories      { get; set; }
    public int  TotalOrders          { get; set; }
    public int  PendingOrders        { get; set; }
    public int  TotalDealers         { get; set; }
    public decimal MonthlyRevenue    { get; set; }
    public decimal TotalRevenue      { get; set; }

    // ─── Grafik Verileri ────────────────────────────────────────────────────────
    /// <summary>Aylık satış rakamları – Chart.js çizgi grafik için.</summary>
    public List<decimal> MonthlySalesData  { get; set; } = [];
    public List<string>  MonthLabels       { get; set; } = [];

    /// <summary>Kategori bazlı satış – pasta grafik.</summary>
    public List<string>  CategoryLabels    { get; set; } = [];
    public List<int>     CategorySalesData { get; set; } = [];

    // ─── Liste Verileri ─────────────────────────────────────────────────────────
    public List<RecentProductItem>   RecentProducts  { get; set; } = [];
    public List<RecentOrderItem>     RecentOrders    { get; set; } = [];
    public List<TopDealerItem>       TopDealers      { get; set; } = [];
    public List<CriticalProductItem> CriticalProducts { get; set; } = [];
}

public class RecentProductItem
{
    public int    Id        { get; set; }
    public string Name      { get; set; } = string.Empty;
    public string SKU       { get; set; } = string.Empty;
    public string Category  { get; set; } = string.Empty;
    public decimal Price    { get; set; }
    public int    Stock     { get; set; }
    public bool   IsCritical { get; set; }
}

public class RecentOrderItem
{
    public int    Id          { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string DealerName  { get; set; } = string.Empty;
    public decimal Total      { get; set; }
    public string Status      { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TopDealerItem
{
    public string DealerName   { get; set; } = string.Empty;
    public string CompanyName  { get; set; } = string.Empty;
    public int    OrderCount   { get; set; }
    public decimal TotalSpent  { get; set; }
    public string Tier         { get; set; } = string.Empty;
}

public class CriticalProductItem
{
    public int    Id           { get; set; }
    public string Name         { get; set; } = string.Empty;
    public int    StockCount   { get; set; }
    public int    CriticalLevel { get; set; }
}
