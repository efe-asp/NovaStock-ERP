using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using NovaStock.Web.Data;
using NovaStock.Web.Hubs;
using NovaStock.Web.Models;
using NovaStock.Web.Services;
using NovaStock.Web.ViewModels;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Ürün CRUD, Excel Export/Import, kritik stok alarmı, Soft Delete geri yükleme.
/// Admin: Tüm işlemler | Dealer: Sadece görüntüleme
/// </summary>
[Authorize]
public class ProductController : Controller
{
    private readonly ApplicationDbContext     _context;
    private readonly ExcelService             _excel;
    private readonly IEmailService            _email;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IConfiguration           _config;
    private readonly ILogger<ProductController> _logger;

    public ProductController(
        ApplicationDbContext          context,
        ExcelService                  excel,
        IEmailService                 email,
        IHubContext<NotificationHub>  hub,
        IConfiguration                config,
        ILogger<ProductController>    logger)
    {
        _context = context;
        _excel   = excel;
        _email   = email;
        _hub     = hub;
        _config  = config;
        _logger  = logger;
    }

    // ─── INDEX – Listeleme + Dinamik LINQ Filtreleme ────────────────────────────
    public async Task<IActionResult> Index([FromQuery] ProductFilterViewModel filter)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .AsQueryable();

        // ── Dinamik Filtreleme (Smart Search) ──────────────────────────────────
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(search) ||
                p.SKU.ToLower().Contains(search)  ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(search)) ||
                (p.Description != null && p.Description.ToLower().Contains(search)));
        }

        if (filter.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == filter.CategoryId.Value);

        if (filter.MinPrice.HasValue)
            query = query.Where(p => p.BasePrice >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            query = query.Where(p => p.BasePrice <= filter.MaxPrice.Value);

        if (filter.InStockOnly)
            query = query.Where(p => p.StockCount > 0);

        if (filter.CriticalOnly)
            query = query.Where(p => p.StockCount <= p.CriticalStockLevel);

        // ── Sıralama ───────────────────────────────────────────────────────────
        query = (filter.SortBy, filter.SortOrder) switch
        {
            ("Price",    "asc")  => query.OrderBy(p => p.BasePrice),
            ("Price",    "desc") => query.OrderByDescending(p => p.BasePrice),
            ("Stock",    "asc")  => query.OrderBy(p => p.StockCount),
            ("Stock",    "desc") => query.OrderByDescending(p => p.StockCount),
            ("Category", _)      => query.OrderBy(p => p.Category.Name),
            ("Name",     "desc") => query.OrderByDescending(p => p.Name),
            _                    => query.OrderBy(p => p.Name)
        };

        // ── Sayfalama ─────────────────────────────────────────────────────────
        var totalCount = await query.CountAsync();
        var products   = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        ViewBag.Categories  = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");
        ViewBag.Filter      = filter;
        ViewBag.TotalCount  = totalCount;
        ViewBag.TotalPages  = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

        return View(products);
    }

    // ─── DETAIL ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Detail(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Include(p => p.ProductWarehouses).ThenInclude(pw => pw.Warehouse)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null) return NotFound();

        // JSON özelliklerini parse et
        var specs = new ProductSpecsData();
        if (!string.IsNullOrEmpty(product.SpecsJson))
        {
            try { specs = JsonSerializer.Deserialize<ProductSpecsData>(product.SpecsJson) ?? new(); }
            catch { specs = new(); }
        }

        // Son 6 aylık satış verisi
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var salesData = await _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.ProductId == id && oi.Order.CreatedAt >= sixMonthsAgo)
            .GroupBy(oi => new { oi.Order.CreatedAt.Year, oi.Order.CreatedAt.Month })
            .Select(g => new {
                Year     = g.Key.Year,
                Month    = g.Key.Month,
                Quantity = g.Sum(oi => oi.Quantity),
                Revenue  = g.Sum(oi => oi.Quantity * oi.UnitPrice)
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        var monthlyNames = new[] { "Oca", "Şub", "Mar", "Nis", "May", "Haz",
                                   "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };
        var monthlySales = salesData.Select(s => new MonthlySalesStat
        {
            Month    = monthlyNames[s.Month - 1],
            Quantity = s.Quantity,
            Revenue  = s.Revenue
        }).ToList();

        // Son 10 sipariş (bu ürün)
        var recentOrders = await _context.OrderItems
            .Include(oi => oi.Order).ThenInclude(o => o.Dealer)
            .Where(oi => oi.ProductId == id)
            .OrderByDescending(oi => oi.Order.CreatedAt)
            .Take(10)
            .Select(oi => new ProductRecentOrderItem
            {
                OrderId     = oi.OrderId,
                OrderNumber = oi.Order.OrderNumber,
                DealerName  = oi.Order.Dealer.FullName,
                Quantity    = oi.Quantity,
                UnitPrice   = oi.UnitPrice,
                OrderDate   = oi.Order.CreatedAt,
                Status      = oi.Order.Status.ToString()
            })
            .ToListAsync();

        var vm = new ProductDetailViewModel
        {
            Product      = product,
            Specs        = specs,
            MonthlySales = monthlySales,
            RecentOrders = recentOrders
        };

        return View(vm);
    }

    // ─── FETCH SPECS (Phone Specs API Proxy) ─────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> FetchSpecs(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { error = "Model adı boş olamaz." });

        try
        {
            using var http   = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var encoded      = Uri.EscapeDataString(query);
            var searchUrl    = $"https://phone-specs-api.vercel.app/search?query={encoded}";
            var searchJson   = await http.GetStringAsync(searchUrl);
            using var doc    = JsonDocument.Parse(searchJson);
            var root         = doc.RootElement;

            // API'den ilk sonucu al
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return Ok(new { found = false, message = "Sonuç bulunamadı." });

            var phones = data.EnumerateArray().ToList();
            if (phones.Count == 0)
                return Ok(new { found = false, message = "Sonuç bulunamadı." });

            var firstPhone = phones[0];
            if (!firstPhone.TryGetProperty("slug", out var slugEl))
                return Ok(new { found = false });

            var slug       = slugEl.GetString();
            var detailUrl  = $"https://phone-specs-api.vercel.app/{slug}";
            var detailJson = await http.GetStringAsync(detailUrl);
            using var detailDoc = JsonDocument.Parse(detailJson);
            var detail          = detailDoc.RootElement;

            if (!detail.TryGetProperty("data", out var phoneData))
                return Ok(new { found = false });

            // Görseli al
            phoneData.TryGetProperty("thumbnail", out var thumbEl);
            var thumbnail = thumbEl.ValueKind == JsonValueKind.String ? thumbEl.GetString() : null;

            // Teknik özellikleri düzleştir
            var techSpecs = new Dictionary<string, string>();
            if (phoneData.TryGetProperty("specifications", out var specsEl))
            {
                foreach (var spec in specsEl.EnumerateArray())
                {
                    if (!spec.TryGetProperty("specs", out var specItems)) continue;
                    foreach (var item in specItems.EnumerateArray())
                    {
                        item.TryGetProperty("name", out var nameEl);
                        item.TryGetProperty("value", out var valEl);
                        var key = nameEl.ValueKind == JsonValueKind.String ? nameEl.GetString()! : "";
                        var val = valEl.ValueKind == JsonValueKind.Array
                            ? string.Join(", ", valEl.EnumerateArray().Select(v => v.GetString()))
                            : (valEl.ValueKind == JsonValueKind.String ? valEl.GetString() ?? "" : "");
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                            techSpecs[key] = val;
                    }
                }
            }

            phoneData.TryGetProperty("phone_name", out var phoneNameEl);

            return Ok(new
            {
                found     = true,
                phoneName = phoneNameEl.ValueKind == JsonValueKind.String ? phoneNameEl.GetString() : query,
                thumbnail,
                specs     = techSpecs
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("FetchSpecs API hatası: {Error}", ex.Message);
            return Ok(new { found = false, message = "API bağlantı hatası: " + ex.Message });
        }
    }

    // ─── RECYCLE BIN – Silinen ürünleri listele ──────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Recycle()
    {
        var deleted = await _context.Products
            .IgnoreQueryFilters()
            .Include(p => p.Category)
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .ToListAsync();

        return View(deleted);
    }

    // ─── RESTORE – Soft-deleted ürünü geri yükle ────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null) return NotFound();

        product.IsDeleted = false;
        product.DeletedAt = null;
        product.DeletedBy = null;

        await _context.SaveChangesAsync();
        TempData["Success"] = $"'{product.Name}' ürünü başarıyla geri yüklendi.";
        return RedirectToAction(nameof(Recycle));
    }

    // ─── CREATE GET ──────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        var vm = new ProductFormViewModel
        {
            Categories = await GetCategoriesAsync(),
            Suppliers  = await GetSuppliersAsync()
        };
        return View(vm);
    }

    // ─── CREATE POST ─────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Categories = await GetCategoriesAsync();
            vm.Suppliers  = await GetSuppliersAsync();
            return View(vm);
        }

        var product = new Product
        {
            Name               = vm.Name,
            SKU                = vm.SKU,
            Barcode            = vm.Barcode,
            Description        = vm.Description,
            BasePrice          = vm.BasePrice,
            CostPrice          = vm.CostPrice,
            VatRate            = vm.VatRate,
            StockCount         = vm.StockCount,
            CriticalStockLevel = vm.CriticalStockLevel,
            CategoryId         = vm.CategoryId,
            SupplierId         = vm.SupplierId,
            IsActive           = vm.IsActive,
            ImageUrl           = vm.ImageUrl,
            SpecsJson          = vm.SpecsJson
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Kritik stok kontrolü
        await CheckCriticalStockAsync(product);

        TempData["Success"] = $"'{product.Name}' ürünü başarıyla eklendi.";
        return RedirectToAction(nameof(Index));
    }

    // ─── EDIT GET ────────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is null) return NotFound();

        var vm = new ProductFormViewModel
        {
            Id                 = product.Id,
            Name               = product.Name,
            SKU                = product.SKU,
            Barcode            = product.Barcode,
            Description        = product.Description,
            BasePrice          = product.BasePrice,
            CostPrice          = product.CostPrice,
            VatRate            = product.VatRate,
            StockCount         = product.StockCount,
            CriticalStockLevel = product.CriticalStockLevel,
            CategoryId         = product.CategoryId,
            SupplierId         = product.SupplierId,
            IsActive           = product.IsActive,
            ImageUrl           = product.ImageUrl,
            SpecsJson          = product.SpecsJson,
            Categories         = await GetCategoriesAsync(),
            Suppliers          = await GetSuppliersAsync()
        };

        return View(vm);
    }

    // ─── EDIT POST ───────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            vm.Categories = await GetCategoriesAsync();
            vm.Suppliers  = await GetSuppliersAsync();
            return View(vm);
        }

        var product = await _context.Products.FindAsync(id);
        if (product is null) return NotFound();

        product.Name               = vm.Name;
        product.SKU                = vm.SKU;
        product.Barcode            = vm.Barcode;
        product.Description        = vm.Description;
        product.BasePrice          = vm.BasePrice;
        product.CostPrice          = vm.CostPrice;
        product.VatRate            = vm.VatRate;
        product.StockCount         = vm.StockCount;
        product.CriticalStockLevel = vm.CriticalStockLevel;
        product.CategoryId         = vm.CategoryId;
        product.SupplierId         = vm.SupplierId;
        product.IsActive           = vm.IsActive;
        product.ImageUrl           = vm.ImageUrl;
        product.SpecsJson          = vm.SpecsJson;

        await _context.SaveChangesAsync();

        // Kritik stok sonrası kontrol
        await CheckCriticalStockAsync(product);

        TempData["Success"] = $"'{product.Name}' ürünü güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // ─── DELETE POST (Soft Delete) ──────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is null) return NotFound();

        _context.Products.Remove(product); // SaveChanges override Soft Delete yapar
        await _context.SaveChangesAsync();

        TempData["Success"] = $"'{product.Name}' geri dönüşüm kutusuna taşındı.";
        return RedirectToAction(nameof(Index));
    }

    // ─── EXCEL EXPORT ────────────────────────────────────────────────────────────
    public async Task<IActionResult> ExportExcel([FromQuery] ProductFilterViewModel filter)
    {
        var products = await _context.Products.Include(p => p.Category).ToListAsync();
        var bytes    = _excel.ExportProductsToExcel(products);

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"NovaStock_Stok_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    // ─── EXCEL IMPORT ────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportExcel(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Lütfen geçerli bir Excel dosyası seçin.";
            return RedirectToAction(nameof(Index));
        }

        var categoryMap = await _context.Categories
            .ToDictionaryAsync(c => c.Name, c => c.Id);

        using var stream  = file.OpenReadStream();
        var (products, errors) = _excel.ImportProductsFromExcel(stream, categoryMap);

        if (errors.Count > 0)
        {
            TempData["Error"] = string.Join(" | ", errors.Take(5));
        }

        if (products.Count > 0)
        {
            _context.Products.AddRange(products);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{products.Count} ürün başarıyla içe aktarıldı.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ─── Yardımcı Metotlar ────────────────────────────────────────────────────────
    private async Task<List<CategorySelectItem>> GetCategoriesAsync()
        => await _context.Categories
            .Select(c => new CategorySelectItem { Id = c.Id, Name = c.Name })
            .ToListAsync();

    private async Task<List<SupplierSelectItem>> GetSuppliersAsync()
        => await _context.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierSelectItem { Id = s.Id, Name = s.Name })
            .ToListAsync();

    /// <summary>Kritik stok kontrolü – eşik altındaysa e-posta + SignalR bildirim.</summary>
    private async Task CheckCriticalStockAsync(Product product)
    {
        if (product.StockCount > product.CriticalStockLevel) return;

        var adminEmail = _config["Email:AdminEmail"] ?? "admin@novastock.com";

        // Bildirimi DB'ye kaydet
        var notif = new Notification
        {
            Title = "⚠️ Kritik Stok!",
            Message = $"{product.Name}: {product.StockCount} adet kaldı",
            Type = "stock",
            IconClass = "fa-triangle-exclamation"
        };
        _context.Notifications.Add(notif);
        await _context.SaveChangesAsync();

        // Asenkron e-posta gönder (fire-and-forget)
        _ = _email.SendCriticalStockAlertAsync(product.Name, product.StockCount, adminEmail);

        // SignalR ile admin paneline anlık bildirim
        await _hub.Clients.Group("Admins").SendAsync("ReceiveStockAlert", new
        {
            ProductName    = product.Name,
            RemainingStock = product.StockCount,
            Timestamp      = DateTime.Now.ToString("HH:mm")
        });

        _logger.LogWarning("Kritik stok: {Product} – {Stock} adet kaldı.", product.Name, product.StockCount);
    }

    // ─── FİYAT MATRİSİ EXCEL EXPORT (Dealer) ─────────────────────────────────────────────
    /// <summary>
    /// Bayiye özel tier indirimli fiyat listesini Excel olarak indirir.
    /// Gold: %15 | Silver: %8 | Bronze: Liste fiyatı.
    /// </summary>
    [Authorize(Roles = "Dealer,DealerPurchase")]
    public async Task<IActionResult> ExportPriceList()
    {
        var currentUser = await HttpContext.RequestServices
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>()
            .GetUserAsync(User);

        if (currentUser is null) return Challenge();

        // Sub-user ise ana bayinin tier’ını kullan
        var dealer = currentUser;
        if (!string.IsNullOrEmpty(currentUser.ParentDealerId))
        {
            var parent = await HttpContext.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>()
                .FindByIdAsync(currentUser.ParentDealerId);
            if (parent != null) dealer = parent;
        }

        var products = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Category!.Name)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var bytes    = _excel.ExportPriceMatrix(products, dealer.Tier, dealer.CompanyName);
        var fileName = $"FiyatListesi_{dealer.CompanyName.Replace(" ", "_")}_{dealer.Tier}_{DateTime.Now:yyyyMMdd}.xlsx";

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}

