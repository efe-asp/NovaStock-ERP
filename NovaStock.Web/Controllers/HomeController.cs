using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.Models;
using NovaStock.Web.ViewModels;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Dashboard – Yönetim kokpiti.
/// Admin girişinde admin özet paneli, Bayi girişinde müşteri portalı gösterilir.
/// </summary>
[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext          _context;
    private readonly UserManager<ApplicationUser>  _userManager;
    private readonly ILogger<HomeController>       _logger;

    public HomeController(
        ApplicationDbContext         context,
        UserManager<ApplicationUser> userManager,
        ILogger<HomeController>      logger)
    {
        _context     = context;
        _userManager = userManager;
        _logger      = logger;
    }

    // ─── INDEX ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        // Bayi rolündeyse kişisel dashboard'a yönlendir
        if (User.IsInRole("Bayi"))
            return await BuildDealerDashboard();

        return await BuildAdminDashboard();
    }

    // ─── ADMIN DASHBOARD ────────────────────────────────────────────────────────
    private async Task<IActionResult> BuildAdminDashboard()
    {
        var now          = DateTime.UtcNow;
        var monthStart   = new DateTime(now.Year, now.Month, 1);
        var last12Months = now.AddMonths(-11);

        // ─── Özet Sayılar ────────────────────────────────────────────────────────
        var totalProducts      = await _context.Products.CountAsync();
        var criticalCount      = await _context.Products.CountAsync(p => p.StockCount <= p.CriticalStockLevel);
        var totalCategories    = await _context.Categories.CountAsync();
        var totalOrders        = await _context.Orders.CountAsync();
        var pendingOrders      = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
        var totalDealers       = await _context.Users.CountAsync();
        var monthlyRevenue     = await _context.Orders
            .Where(o => o.CreatedAt >= monthStart && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;
        var totalRevenue       = await _context.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;

        // ─── Aylık Satış Verisi (son 12 ay) ─────────────────────────────────────
        var monthlySales = await _context.Orders
            .Where(o => o.CreatedAt >= last12Months && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Sum(o => o.Total)
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        var monthLabels      = new List<string>();
        var monthlySalesData = new List<decimal>();
        for (int i = 11; i >= 0; i--)
        {
            var d     = now.AddMonths(-i);
            var label = d.ToString("MMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
            var sale  = monthlySales.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
            monthLabels.Add(label);
            monthlySalesData.Add(sale?.Total ?? 0m);
        }

        // ─── Kategori Bazlı Satış (pasta grafik) ────────────────────────────────
        var categorySales = await _context.OrderItems
            .Include(oi => oi.Product).ThenInclude(p => p.Category)
            .GroupBy(oi => oi.Product.Category.Name)
            .Select(g => new { Category = g.Key, Count = g.Sum(oi => oi.Quantity) })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToListAsync();

        // ─── Son Eklenen 5 Ürün ──────────────────────────────────────────────────
        var recentProducts = await _context.Products
            .Include(p => p.Category)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new RecentProductItem
            {
                Id         = p.Id,
                Name       = p.Name,
                SKU        = p.SKU,
                Category   = p.Category.Name,
                Price      = p.BasePrice,
                Stock      = p.StockCount,
                IsCritical = p.StockCount <= p.CriticalStockLevel
            })
            .ToListAsync();

        // ─── Son 5 Sipariş ───────────────────────────────────────────────────────
        var recentOrders = await _context.Orders
            .Include(o => o.Dealer)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new RecentOrderItem
            {
                Id          = o.Id,
                OrderNumber = o.OrderNumber,
                DealerName  = o.Dealer.FullName,
                Total       = o.Total,
                Status      = o.Status.ToString(),
                CreatedAt   = o.CreatedAt
            })
            .ToListAsync();

        // ─── Top 3 Bayi ──────────────────────────────────────────────────────────
        var topDealers = await _context.Orders
            .Include(o => o.Dealer)
            .GroupBy(o => new { o.DealerId, o.Dealer.FullName, o.Dealer.CompanyName, o.Dealer.Tier })
            .Select(g => new TopDealerItem
            {
                DealerName  = g.Key.FullName,
                CompanyName = g.Key.CompanyName,
                OrderCount  = g.Count(),
                TotalSpent  = g.Sum(o => o.Total),
                Tier        = g.Key.Tier.ToString()
            })
            .OrderByDescending(x => x.TotalSpent)
            .Take(3)
            .ToListAsync();

        // ─── Kritik Stoklu Ürünler ───────────────────────────────────────────────
        var criticalProducts = await _context.Products
            .Where(p => p.StockCount <= p.CriticalStockLevel)
            .Select(p => new CriticalProductItem
            {
                Id            = p.Id,
                Name          = p.Name,
                StockCount    = p.StockCount,
                CriticalLevel = p.CriticalStockLevel
            })
            .OrderBy(p => p.StockCount)
            .Take(10)
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            TotalProducts      = totalProducts,
            CriticalStockCount = criticalCount,
            TotalCategories    = totalCategories,
            TotalOrders        = totalOrders,
            PendingOrders      = pendingOrders,
            TotalDealers       = totalDealers,
            MonthlyRevenue     = monthlyRevenue,
            TotalRevenue       = totalRevenue,
            MonthLabels        = monthLabels,
            MonthlySalesData   = monthlySalesData,
            CategoryLabels     = categorySales.Select(x => x.Category).ToList(),
            CategorySalesData  = categorySales.Select(x => x.Count).ToList(),
            RecentProducts     = recentProducts,
            RecentOrders       = recentOrders,
            TopDealers         = topDealers,
            CriticalProducts   = criticalProducts
        };

        return View(vm);
    }

    // ─── BAYI DASHBOARD ─────────────────────────────────────────────────────────
    private async Task<IActionResult> BuildDealerDashboard()
    {
        var currentUser  = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        var dealerId     = currentUser.Id;
        var now          = DateTime.UtcNow;
        var last12Months = now.AddMonths(-11);

        // ── 1. Katalog: Aktif ürün sayısı ────────────────────────────────────────
        var catalogCount = await _context.Products.CountAsync(p => p.IsActive);

        // ── 2. Kargodaki siparişlerim ─────────────────────────────────────────────
        var shippedCount = await _context.Orders
            .CountAsync(o => o.DealerId == dealerId && o.Status == OrderStatus.Shipped);

        // ── 3. Açık siparişlerim (Bekleyen + Onaylanan) ───────────────────────────
        var openCount = await _context.Orders
            .CountAsync(o => o.DealerId == dealerId &&
                             (o.Status == OrderStatus.Pending || o.Status == OrderStatus.Approved));

        // ── 4. Güncel bakiye (Teslim edilmiş siparişlerin toplamı = borç) ─────────
        var currentBalance = await _context.Orders
            .Where(o => o.DealerId == dealerId && o.Status == OrderStatus.Delivered)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;

        // ── 5. Kredi limiti: Tier'a göre sabit eşik ──────────────────────────────
        var creditLimit = currentUser.Tier switch
        {
            DealerTier.Gold   => 500_000m,
            DealerTier.Silver => 200_000m,
            _                 => 50_000m    // Bronze
        };

        // ── 6. İndirim oranı ──────────────────────────────────────────────────────
        var discountRate = currentUser.Tier switch
        {
            DealerTier.Gold   => 15m,
            DealerTier.Silver => 10m,
            _                 => 5m         // Bronze
        };

        // ── 7. Aylık satın alma trendi (son 12 ay – sadece bu bayi) ───────────────
        var monthlyRaw = await _context.Orders
            .Where(o => o.DealerId == dealerId
                     && o.CreatedAt >= last12Months
                     && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(o => o.Total) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        var monthLabels      = new List<string>();
        var monthlyPurchase  = new List<decimal>();
        for (int i = 11; i >= 0; i--)
        {
            var d     = now.AddMonths(-i);
            var label = d.ToString("MMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
            var sale  = monthlyRaw.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
            monthLabels.Add(label);
            monthlyPurchase.Add(sale?.Total ?? 0m);
        }

        var totalPurchase = monthlyPurchase.Sum();

        // ── 8. Kategori dağılımı (bu bayinin siparişleri) ─────────────────────────
        var categorySales = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product).ThenInclude(p => p.Category)
            .Where(oi => oi.Order.DealerId == dealerId
                      && oi.Order.Status   != OrderStatus.Cancelled)
            .GroupBy(oi => oi.Product.Category.Name)
            .Select(g => new { Category = g.Key, Count = g.Sum(oi => oi.Quantity) })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToListAsync();

        // ── 9. Sık satın aldığım ürünler ──────────────────────────────────────────
        var frequentProducts = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product).ThenInclude(p => p.Category)
            .Where(oi => oi.Order.DealerId == dealerId
                      && oi.Order.Status   != OrderStatus.Cancelled)
            .GroupBy(oi => new
            {
                oi.ProductId,
                oi.Product.Name,
                oi.Product.SKU,
                CategoryName = oi.Product.Category.Name,
                oi.Product.BasePrice
            })
            .Select(g => new DealerFavoriteProduct
            {
                ProductId   = g.Key.ProductId,
                ProductName = g.Key.Name,
                SKU         = g.Key.SKU,
                Category    = g.Key.CategoryName,
                TotalQty    = g.Sum(oi => oi.Quantity),
                UnitPrice   = g.Key.BasePrice
            })
            .OrderByDescending(x => x.TotalQty)
            .Take(5)
            .ToListAsync();

        // ── 10. Son siparişlerim ──────────────────────────────────────────────────
        var myRecentOrders = await _context.Orders
            .Where(o => o.DealerId == dealerId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new DealerRecentOrderItem
            {
                Id          = o.Id,
                OrderNumber = o.OrderNumber,
                Total       = o.Total,
                Status      = o.Status.ToString(),
                CreatedAt   = o.CreatedAt
            })
            .ToListAsync();

        var dealerVm = new DealerDashboardViewModel
        {
            CatalogProductCount  = catalogCount,
            ShippedOrderCount    = shippedCount,
            Tier                 = currentUser.Tier,
            DealerDiscountRate   = discountRate,
            OpenOrderCount       = openCount,
            CurrentBalance       = currentBalance,
            CreditLimit          = creditLimit,
            MonthLabels          = monthLabels,
            MonthlyPurchaseData  = monthlyPurchase,
            TotalPurchase        = totalPurchase,
            CategoryLabels       = categorySales.Select(x => x.Category).ToList(),
            CategoryPurchaseData = categorySales.Select(x => x.Count).ToList(),
            MyRecentOrders       = myRecentOrders,
            FrequentProducts     = frequentProducts
        };

        var vm = new DashboardViewModel { DealerDashboard = dealerVm };
        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}

