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
    private readonly PromotionEngine              _promotionEngine;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<OrderController>     _logger;

    public OrderController(
        ApplicationDbContext          context,
        UserManager<ApplicationUser>  userManager,
        PdfService                    pdf,
        PromotionEngine               promotionEngine,
        IHubContext<NotificationHub>  hub,
        ILogger<OrderController>      logger)
    {
        _context         = context;
        _userManager     = userManager;
        _pdf             = pdf;
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
        [FromForm] string?      notes)
    {
        if (productIds.Count == 0)
        {
            ModelState.AddModelError("", "En az bir ürün seçiniz.");
            return RedirectToAction(nameof(Create));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();

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
            var unitPrice = product.GetPriceForTier(user.Tier);

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

        var result = _promotionEngine.Apply(items, activePromos, user);

        // Sipariş numarası üret
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        var order = new Order
        {
            OrderNumber    = orderNumber,
            DealerId       = user.Id,
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
            Message = $"{user.FullName} - {result.Total:N2} ₺",
            Type = "order",
            IconClass = "fa-cart-check"
        };
        _context.Notifications.Add(notif);

        await _context.SaveChangesAsync();

        // SignalR – Admin paneline anlık bildirim
        await _hub.Clients.Group("Admins").SendAsync("ReceiveOrderNotification", new
        {
            DealerName = user.FullName,
            Total      = result.Total.ToString("N2"),
            OrderId    = order.Id,
            Timestamp  = DateTime.Now.ToString("HH:mm")
        });

        _logger.LogInformation("Yeni sipariş: {OrderNumber}, Bayi: {Dealer}, Toplam: {Total}",
            orderNumber, user.FullName, result.Total);

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
}
