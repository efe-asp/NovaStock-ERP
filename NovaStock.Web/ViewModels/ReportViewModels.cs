namespace NovaStock.Web.ViewModels;

// ─── Satış Analitiği ─────────────────────────────────────────────────────────
public class SalesReportViewModel
{
    public decimal TotalRevenue       { get; set; }
    public decimal MonthRevenue       { get; set; }
    public int     TotalOrders        { get; set; }
    public decimal AverageOrderValue  { get; set; }

    // Aylık satış – çizgi grafik
    public List<string>  MonthLabels     { get; set; } = [];
    public List<decimal> MonthlySales    { get; set; } = [];

    // Kategori bazlı ciro – bar grafik
    public List<string>  CategoryLabels  { get; set; } = [];
    public List<decimal> CategoryRevenue { get; set; } = [];

    // En çok satan 10 ürün
    public List<TopProductItem>  TopProducts  { get; set; } = [];

    // En çok sipariş veren 5 bayi
    public List<TopDealerItem>   TopDealers   { get; set; } = [];
}

public class TopProductItem
{
    public int     ProductId   { get; set; }
    public string  Name        { get; set; } = string.Empty;
    public string  Category    { get; set; } = string.Empty;
    public int     TotalSold   { get; set; }
    public decimal TotalRevenue { get; set; }
}

// ─── Stok Değer Raporu ───────────────────────────────────────────────────────
public class StockValueViewModel
{
    public decimal TotalStockValue    { get; set; }
    public int     TotalProductCount  { get; set; }
    public int     CriticalCount      { get; set; }
    public int     OutOfStockCount    { get; set; }

    public List<StockValueCategoryRow> ByCategory { get; set; } = [];
    public List<StockValueProductRow>  Products   { get; set; } = [];
}

public class StockValueCategoryRow
{
    public string  Category   { get; set; } = string.Empty;
    public int     ProductCount { get; set; }
    public int     TotalStock { get; set; }
    public decimal TotalValue { get; set; }
}

public class StockValueProductRow
{
    public int     Id         { get; set; }
    public string  Name       { get; set; } = string.Empty;
    public string  SKU        { get; set; } = string.Empty;
    public string  Category   { get; set; } = string.Empty;
    public int     Stock      { get; set; }
    public decimal BasePrice  { get; set; }
    public decimal TotalValue { get; set; }
    public bool    IsCritical { get; set; }
}

// ─── Sistem Ayarları ─────────────────────────────────────────────────────────
public class SettingsViewModel
{
    // SMTP
    public string SmtpHost     { get; set; } = string.Empty;
    public string SmtpPort     { get; set; } = "587";
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpFromName { get; set; } = string.Empty;
    public string AdminEmail   { get; set; } = string.Empty;

    // Sistem
    public int    CriticalStockDefaultThreshold { get; set; } = 5;
    public string AppVersion { get; set; } = "1.0";
    public string DatabasePath { get; set; } = string.Empty;

    // İstatistik (readonly)
    public int    TotalProducts  { get; set; }
    public int    TotalOrders    { get; set; }
    public int    TotalUsers     { get; set; }
    public int    TotalAuditLogs { get; set; }
    public long   DbSizeBytes    { get; set; }
}
