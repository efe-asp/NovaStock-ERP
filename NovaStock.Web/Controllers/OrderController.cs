using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using NovaStock.Web.Data;
using NovaStock.Web.Hubs;
using NovaStock.Web.Models;
using NovaStock.Web.Extensions;
using NovaStock.Web.Services;
using NovaStock.Web.ViewModels;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Sipariş yönetimi: Bayi sipariş verir, Admin onaylar.
/// PDF fatura indirme, SignalR anlık bildirim, Promotion Engine entegrasyonu.
/// </summary>
[Authorize]
public class OrderController : Controller
{
    private readonly ApplicationDbContext         _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PdfService                   _pdf;
    private readonly ExcelService                 _excel;
    private readonly PromotionEngine              _promotionEngine;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<OrderController>     _logger;

    public OrderController(
        ApplicationDbContext          context,
        UserManager<ApplicationUser>  userManager,
        PdfService                    pdf,
        ExcelService                  excel,
        PromotionEngine               promotionEngine,
        IHubContext<NotificationHub>  hub,
        ILogger<OrderController>      logger)
    {
        _context         = context;
        _userManager     = userManager;
        _pdf             = pdf;
        _excel           = excel;
        _promotionEngine = promotionEngine;
        _hub             = hub;
        _logger          = logger;
    }

    // ─── INDEX ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index([FromQuery] string? status = null)
    {
        var user    = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole("Admin");

        var query = _context.Orders
            .Include(o => o.Dealer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .AsQueryable();

        if (!isAdmin)
            query = query.Where(o => o.DealerId == user!.Id);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            query = query.Where(o => o.Status == orderStatus);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        
        ViewBag.CurrentStatus = status;

        return View(orders);
    }

    // ─── DETAIL ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Detail(int id)
    {
        var user    = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole("Admin");

        var order = await _context.Orders
            .Include(o => o.Dealer)
            .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
            .Include(o => o.Warehouse)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();
        if (!isAdmin && order.DealerId != user!.Id) return Forbid();

        return View(order);
    }

    // ─── CREATE GET (Bayi sipariş oluşturma formu) ───────────────────────────────
    [Authorize(Roles = "Dealer,Admin")]
    public async Task<IActionResult> Create()
    {
        var products   = await _context.Products.Include(p => p.Category).ToListAsync();
        var warehouses = await _context.Warehouses.ToListAsync();
        var suppliers  = await _context.Suppliers.ToListAsync();

        if (User.IsInRole("Admin"))
        {
            var dealers = await _userManager.GetUsersInRoleAsync("Dealer");
            ViewBag.Dealers = dealers.Where(d => d.IsActive).ToList();
        }

        ViewBag.Suppliers  = suppliers;
        ViewBag.Products   = products;
        ViewBag.Warehouses = warehouses;
        return View();
    }

    // ─── CREATE POST ─────────────────────────────────────────────────────────────
    [Authorize(Roles = "Dealer,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] List<int>    productIds,
        [FromForm] List<int>    quantities,
        [FromForm] int?         warehouseId,
        [FromForm] string?      dealerId,
        [FromForm] string?      notes)
    {
        if (productIds.Count == 0)
        {
            ModelState.AddModelError("", "En az bir ürün seçiniz.");
            return RedirectToAction(nameof(Create));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        var orderDealer = user;
        if (User.IsInRole("Admin"))
        {
            if (string.IsNullOrEmpty(dealerId))
            {
                TempData["Error"] = "Lütfen bir bayi seçiniz.";
                return RedirectToAction(nameof(Create));
            }
            var selectedDealer = await _userManager.FindByIdAsync(dealerId);
            if (selectedDealer is not null)
                orderDealer = selectedDealer;
        }

        // Sipariş kalemleri oluştur
        var items = new List<OrderItem>();
        for (int i = 0; i < productIds.Count; i++)
        {
            var product = await _context.Products.FindAsync(productIds[i]);
            if (product is null) continue;

            // Stok yeterli mi?
            var qty = quantities.ElementAtOrDefault(i);
            if (qty <= 0 || product.StockCount < qty)
            {
                TempData["Error"] = $"'{product.Name}' için yeterli stok yok.";
                return RedirectToAction(nameof(Create));
            }

            // Bayi kademine göre fiyat
            var unitPrice = product.GetPriceForTier(orderDealer.Tier);

            items.Add(new OrderItem
            {
                ProductId  = product.Id,
                Product    = product,
                Quantity   = qty,
                UnitPrice  = unitPrice
            });

            // Stok düşür
            product.StockCount -= qty;
        }

        // Kampanya motorunu çalıştır
        var activePromos = await _context.Promotions
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .ToListAsync();

        var result = _promotionEngine.Apply(items, activePromos, orderDealer);

        // Sipariş numarası üret
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        var order = new Order
        {
            OrderNumber    = orderNumber,
            DealerId       = orderDealer.Id,
            Status         = OrderStatus.Pending,
            SubTotal       = result.SubTotal,
            DiscountAmount = result.DiscountAmount,
            Total          = result.Total,
            Notes          = notes,
            WarehouseId    = warehouseId,
            Items          = items
        };

        _context.Orders.Add(order);

        // Bildirimi DB'ye kaydet
        var notif = new Notification
        {
            Title = "Yeni Sipariş!",
            Message = $"{orderDealer.FullName} - {result.Total:N2} ₺",
            Type = "order",
            IconClass = "fa-cart-check"
        };
        _context.Notifications.Add(notif);

        await _context.SaveChangesAsync();

        // SignalR – Admin paneline anlık bildirim
        await _hub.Clients.Group("Admins").SendAsync("ReceiveOrderNotification", new
        {
            DealerName = orderDealer.FullName,
            Total      = result.Total.ToString("N2"),
            OrderId    = order.Id,
            Timestamp  = DateTime.Now.ToString("HH:mm")
        });

        _logger.LogInformation("Yeni sipariş: {OrderNumber}, Bayi: {Dealer}, Toplam: {Total}",
            orderNumber, orderDealer.FullName, result.Total);

        TempData["Success"] = $"Siparişiniz ({orderNumber}) başarıyla oluşturuldu.";
        return RedirectToAction(nameof(Detail), new { id = order.Id });
    }

    // ─── APPROVE (Admin) ─────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order is null) return NotFound();

        order.Status = OrderStatus.Approved;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Sipariş #{order.OrderNumber} onaylandı.";
        return RedirectToAction(nameof(Index));
    }

    // ─── CANCEL ──────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var user  = await _userManager.GetUserAsync(User);
        var order = await _context.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();
        if (!User.IsInRole("Admin") && order.DealerId != user!.Id) return Forbid();
        if (order.Status is OrderStatus.Shipped or OrderStatus.Delivered)
        {
            TempData["Error"] = "Teslim edilmiş veya kargodaki sipariş iptal edilemez.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // Stokları geri yükle
        foreach (var item in order.Items)
        {
            if (item.Product is not null)
                item.Product.StockCount += item.Quantity;
        }

        order.Status = OrderStatus.Cancelled;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Sipariş iptal edildi, stoklar geri yüklendi.";
        return RedirectToAction(nameof(Index));
    }

    // ─── DELETE (Soft Delete) ────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();

        // Eğer sipariş iptal edilmemişse, silinmeden önce stokları geri yükle
        if (order.Status != OrderStatus.Cancelled)
        {
            foreach (var item in order.Items)
            {
                if (item.Product is not null)
                    item.Product.StockCount += item.Quantity;
            }
        }

        _context.Orders.Remove(order); // Soft delete tetiklenir
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Sipariş #{order.OrderNumber} başarıyla silindi (Geri Dönüşüm Kutusuna taşındı).";
        return RedirectToAction(nameof(Index));
    }

    // ─── PDF FATURA İNDİR ────────────────────────────────────────────────────────
    public async Task<IActionResult> DownloadInvoice(int id)
    {
        var user  = await _userManager.GetUserAsync(User);
        var order = await _context.Orders
            .Include(o => o.Dealer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Warehouse)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();
        if (!User.IsInRole("Admin") && order.DealerId != user!.Id) return Forbid();

        var pdfBytes = _pdf.GenerateInvoice(order);

        return File(pdfBytes, "application/pdf",
            $"Fatura_{order.OrderNumber}_{DateTime.Now:yyyyMMdd}.pdf");
    }

    // ─── TOPLU EXCEL SİPARİŞ ────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Şablon Excel dosyasını indirir (SKU + Adet kolonları).
    /// </summary>
    [Authorize(Roles = "Dealer,DealerPurchase")]
    public IActionResult BulkOrderTemplate()
    {
        var bytes = _excel.GenerateBulkOrderTemplate();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "NovaStock_TopluSiparis_Sablon.xlsx");
    }

    /// <summary>
    /// Bayinin yüklediği Excel'i okur, stok kontrolu yapar, sonucu gösterir.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Dealer,DealerPurchase")]
    public async Task<IActionResult> BulkOrderProcess(IFormFile? excelFile)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        if (excelFile is null || excelFile.Length == 0)
        {
            TempData["Error"] = "Lütfen geçerli bir Excel dosyası yükleyin.";
            return RedirectToAction(nameof(Index));
        }

        // Excel'i parse et
        using var stream = excelFile.OpenReadStream();
        var (rows, excelErrors) = _excel.ParseBulkOrderExcel(stream);

        var resultItems = new List<BulkOrderResultItem>();

        foreach (var row in rows)
        {
            var product = await _context.Products
                .Where(p => p.SKU == row.SKU && p.IsActive)
                .FirstOrDefaultAsync();

            if (product is null)
            {
                excelErrors.Add($"Satır {row.RowNumber}: '{row.SKU}' SKU kodu sistemde bulunamadı.");
                continue;
            }

            // Tier indirimli fiyat hesapla
            var dealerId = currentUser.ParentDealerId ?? currentUser.Id;
            var dealer   = await _userManager.FindByIdAsync(dealerId) ?? currentUser;
            var discount = dealer.Tier switch
            {
                DealerTier.Gold   => 0.15m,
                DealerTier.Silver => 0.08m,
                _                 => 0m
            };
            var unitPrice = Math.Round(product.BasePrice * (1 - discount), 2);

            resultItems.Add(new BulkOrderResultItem
            {
                SKU         = row.SKU,
                ProductName = product.Name,
                Requested   = row.Quantity,
                Available   = product.StockCount,
                UnitPrice   = unitPrice
            });
        }

        var vm = new BulkOrderResultViewModel
        {
            Items       = resultItems,
            ExcelErrors = excelErrors
        };

        if (!vm.HasErrors && vm.CanProceed)
        {
            // Geçerli satırları sepete ekle (session bazlı sepet veya direk sipariş oluştur)
            // Demo: Sipariş oluşturularak sipariş listesine yönlendir
            TempData["BulkSuccess"] = $"{vm.ValidCount} ürün için sipariş oluşturmaya hazır. Lütfen gözden geçirip onaylayın.³";
        }

        return View("BulkOrderResult", vm);
    }

    // ─── PROFORMA FATURA (Teklif Formu) ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Bayi kendi müşterisine kâr marjlı proforma fatura üretir.
    /// Sipariş ürünlerinin üzerine belirtilen marj eklenerek PDF oluşturulur.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Dealer,DealerPurchase")]
    public async Task<IActionResult> GenerateQuote(int orderId, decimal margin = 10)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        var dealerId = currentUser.ParentDealerId ?? currentUser.Id;
        var dealer   = await _userManager.FindByIdAsync(dealerId) ?? currentUser;

        var order = await _context.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.DealerId == dealerId);

        if (order is null) return NotFound();

        var pdf      = _pdf.GenerateProformaInvoice(order, margin, dealer.CompanyName);
        var fileName = $"ProformaFatura_{dealer.CompanyName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";

        return File(pdf, "application/pdf", fileName);
    }
}

