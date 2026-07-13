using NovaStock.Web.Models;

namespace NovaStock.Web.ViewModels;

/// <summary>
/// Dashboard ana sayfası için veri modeli.
/// Controller tarafından hesaplanır, View'e gönderilir.
/// </summary>
public class DealerDashboardViewModel
{
    // ─── 6 Özet Kart ──────────────────────────────────────────────────────────
    /// <summary>Katalogdaki toplam aktif ürün sayısı.</summary>
    public int      CatalogProductCount  { get; set; }

    /// <summary>Kargoda olan (Shipped) sipariş adedi.</summary>
    public int      ShippedOrderCount    { get; set; }

    /// <summary>Bayi kademesi enum değeri.</summary>
    public DealerTier Tier              { get; set; }

    /// <summary>Kademede uygulanan indirim yüzdesi (Bronze=5, Silver=10, Gold=15).</summary>
    public decimal  DealerDiscountRate   { get; set; }

    /// <summary>Bekleyen + Onaylanan siparişlerin toplam adedi.</summary>
    public int      OpenOrderCount       { get; set; }

    /// <summary>
    /// Bayinin şirkete borcu (pozitif = borç, negatif = alacak).
    /// Tüm teslim edilmiş siparişlerin toplamından oluşur.
    /// </summary>
    public decimal  CurrentBalance       { get; set; }

    /// <summary>Tier'a göre tanımlı kredi limiti.</summary>
    public decimal  CreditLimit          { get; set; }

    // ─── Grafik Verileri ──────────────────────────────────────────────────────
    /// <summary>Son 12 aydaki satın alma tutarları – çizgi grafik.</summary>
    public List<decimal> MonthlyPurchaseData  { get; set; } = [];
    public List<string>  MonthLabels          { get; set; } = [];
    public decimal       TotalPurchase        { get; set; }

    /// <summary>Bu bayinin kategori bazlı sipariş dağılımı – pasta grafik.</summary>
    public List<string>  CategoryLabels       { get; set; } = [];
    public List<int>     CategoryPurchaseData { get; set; } = [];

    // ─── Liste Verileri ───────────────────────────────────────────────────────
    public List<DealerRecentOrderItem>  MyRecentOrders   { get; set; } = [];
    public List<DealerFavoriteProduct>  FrequentProducts { get; set; } = [];
}

public class DealerRecentOrderItem
{
    public int      Id          { get; set; }
    public string   OrderNumber { get; set; } = string.Empty;
    public decimal  Total       { get; set; }
    public string   Status      { get; set; } = string.Empty;
    public DateTime CreatedAt   { get; set; }
}

public class DealerFavoriteProduct
{
    public int      ProductId   { get; set; }
    public string   ProductName { get; set; } = string.Empty;
    public string   SKU         { get; set; } = string.Empty;
    public string   Category    { get; set; } = string.Empty;
    public int      TotalQty    { get; set; }
    public decimal  UnitPrice   { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// ADMIN DASHBOARD VIEW MODEL
// ─────────────────────────────────────────────────────────────────────────────

public class DashboardViewModel
{
    // ─── Özet Kartlar (Admin) ────────────────────────────────────────────────────
    public int  TotalProducts        { get; set; }
    public int  CriticalStockCount   { get; set; }
    public int  TotalCategories      { get; set; }
    public int  TotalOrders          { get; set; }
    public int  PendingOrders        { get; set; }
    public int  TotalDealers         { get; set; }
    public decimal MonthlyRevenue    { get; set; }
    public decimal TotalRevenue      { get; set; }

    // ─── Bayi Dashboard (embed edilmiş, rol kontrolü ile kullanılır) ─────────────
    public DealerDashboardViewModel? DealerDashboard { get; set; }

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
