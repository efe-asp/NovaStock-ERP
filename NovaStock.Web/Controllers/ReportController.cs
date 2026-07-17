using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.Models;
using NovaStock.Web.Services;
using NovaStock.Web.ViewModels;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Raporlar: Satış Analitiği ve Stok Değer Raporu.
/// </summary>
[Authorize]
public class ReportController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ExcelService         _excel;

    public ReportController(ApplicationDbContext context, ExcelService excel)
    {
        _context = context;
        _excel   = excel;
    }

    // ─── SATIŞ ANALİTİĞİ ────────────────────────────────────────────────────────
    public async Task<IActionResult> Sales()
    {
        var now          = DateTime.UtcNow;
        var monthStart   = new DateTime(now.Year, now.Month, 1);
        var last12Months = now.AddMonths(-11);

        // ── Özet rakamlar ──────────────────────────────────────────────────────
        var totalRevenue = await _context.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;

        var monthRevenue = await _context.Orders
            .Where(o => o.CreatedAt >= monthStart && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;

        var totalOrders = await _context.Orders.CountAsync(o => o.Status != OrderStatus.Cancelled);

        var avgOrder = totalOrders > 0 ? totalRevenue / totalOrders : 0m;

        // ── Aylık satış verisi ─────────────────────────────────────────────────
        var monthlySalesRaw = await _context.Orders
            .Where(o => o.CreatedAt >= last12Months && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(o => o.Total) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        var monthLabels  = new List<string>();
        var monthlySales = new List<decimal>();
        for (int i = 11; i >= 0; i--)
        {
            var d     = now.AddMonths(-i);
            var entry = monthlySalesRaw.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
            monthLabels.Add(d.ToString("MMM yyyy", new System.Globalization.CultureInfo("tr-TR")));
            monthlySales.Add(entry?.Total ?? 0m);
        }

        // ── Kategori bazlı ciro ────────────────────────────────────────────────
        var categorySales = await _context.OrderItems
            .Include(oi => oi.Product).ThenInclude(p => p.Category)
            .Where(oi => oi.Order.Status != OrderStatus.Cancelled)
            .GroupBy(oi => oi.Product.Category.Name)
            .Select(g => new
            {
                Category = g.Key,
                Revenue  = g.Sum(oi => oi.UnitPrice * oi.Quantity)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(6)
            .ToListAsync();

        // ── En çok satan ürünler ───────────────────────────────────────────────
        var topProducts = await _context.OrderItems
            .Include(oi => oi.Product).ThenInclude(p => p.Category)
            .GroupBy(oi => new { oi.ProductId, oi.Product.Name, CategoryName = oi.Product.Category.Name })
            .Select(g => new TopProductItem
            {
                ProductId    = g.Key.ProductId,
                Name         = g.Key.Name,
                Category     = g.Key.CategoryName,
                TotalSold    = g.Sum(oi => oi.Quantity),
                TotalRevenue = g.Sum(oi => oi.UnitPrice * oi.Quantity)
            })
            .OrderByDescending(x => x.TotalSold)
            .Take(10)
            .ToListAsync();

        // ── Top bayiler ───────────────────────────────────────────────────────
        var topDealers = await _context.Orders
            .Include(o => o.Dealer)
            .Where(o => o.Status != OrderStatus.Cancelled)
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
            .Take(5)
            .ToListAsync();

        var vm = new SalesReportViewModel
        {
            TotalRevenue      = totalRevenue,
            MonthRevenue      = monthRevenue,
            TotalOrders       = totalOrders,
            AverageOrderValue = avgOrder,
            MonthLabels       = monthLabels,
            MonthlySales      = monthlySales,
            CategoryLabels    = categorySales.Select(x => x.Category).ToList(),
            CategoryRevenue   = categorySales.Select(x => x.Revenue).ToList(),
            TopProducts       = topProducts,
            TopDealers        = topDealers
        };

        return View(vm);
    }

    // ─── STOK DEĞER RAPORU ───────────────────────────────────────────────────────
    public async Task<IActionResult> StockValue()
    {
        var products = await _context.Products
            .Include(p => p.Category)
            .OrderBy(p => p.Category.Name).ThenBy(p => p.Name)
            .ToListAsync();

        var rows = products.Select(p => new StockValueProductRow
        {
            Id         = p.Id,
            Name       = p.Name,
            SKU        = p.SKU,
            Category   = p.Category?.Name ?? "—",
            Stock      = p.StockCount,
            BasePrice  = p.BasePrice,
            TotalValue = p.StockCount * p.BasePrice,
            IsCritical = p.IsCriticalStock
        }).ToList();

        var byCategory = rows
            .GroupBy(r => r.Category)
            .Select(g => new StockValueCategoryRow
            {
                Category     = g.Key,
                ProductCount = g.Count(),
                TotalStock   = g.Sum(r => r.Stock),
                TotalValue   = g.Sum(r => r.TotalValue)
            })
            .OrderByDescending(x => x.TotalValue)
            .ToList();

        var vm = new StockValueViewModel
        {
            TotalStockValue   = rows.Sum(r => r.TotalValue),
            TotalProductCount = rows.Count,
            CriticalCount     = rows.Count(r => r.IsCritical),
            OutOfStockCount   = rows.Count(r => r.Stock == 0),
            ByCategory        = byCategory,
            Products          = rows
        };

        return View(vm);
    }

    // ─── EXCEL EXPORT – Satış ─────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportSalesExcel()
    {
        var orders = await _context.Orders
            .Include(o => o.Dealer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("Satış Raporu");

        // Başlıklar
        ws.Cell(1, 1).Value = "Sipariş No";
        ws.Cell(1, 2).Value = "Bayi";
        ws.Cell(1, 3).Value = "Tarih";
        ws.Cell(1, 4).Value = "Durum";
        ws.Cell(1, 5).Value = "Alt Toplam";
        ws.Cell(1, 6).Value = "İndirim";
        ws.Cell(1, 7).Value = "Toplam";

        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#6366f1");
        headerRow.Style.Font.FontColor       = ClosedXML.Excel.XLColor.White;

        int row = 2;
        foreach (var o in orders)
        {
            ws.Cell(row, 1).Value = o.OrderNumber;
            ws.Cell(row, 2).Value = o.Dealer?.FullName ?? "—";
            ws.Cell(row, 3).Value = o.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            ws.Cell(row, 4).Value = o.Status.ToString();
            ws.Cell(row, 5).Value = (double)o.SubTotal;
            ws.Cell(row, 6).Value = (double)o.DiscountAmount;
            ws.Cell(row, 7).Value = (double)o.Total;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"NovaStock_Satis_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }
}

