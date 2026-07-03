using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.Models;
using NovaStock.Web.ViewModels;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Tedarikçi Yönetimi ve Mal Kabul (Satın Alma) işlemleri. Sadece Admin erişebilir.
/// </summary>
[Authorize(Roles = "Admin")]
public class SupplierController : Controller
{
    private readonly ApplicationDbContext  _context;
    private readonly ILogger<SupplierController> _logger;

    public SupplierController(ApplicationDbContext context, ILogger<SupplierController> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ─── INDEX ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index(string? search)
    {
        var purchaseTotals = await _context.PurchaseOrders
            .Where(po => po.Status == PurchaseOrderStatus.Received)
            .GroupBy(po => po.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(po => po.TotalCost), Count = g.Count() })
            .ToListAsync();

        var suppliersQuery = _context.Suppliers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            suppliersQuery = suppliersQuery.Where(s =>
                s.Name.Contains(search) ||
                (s.ContactPerson != null && s.ContactPerson.Contains(search)) ||
                (s.Email != null && s.Email.Contains(search)));

        var suppliers = await suppliersQuery
            .OrderBy(s => s.Name)
            .ToListAsync();

        var items = suppliers.Select(s =>
        {
            var pt = purchaseTotals.FirstOrDefault(x => x.SupplierId == s.Id);
            return new SupplierListItem
            {
                Id             = s.Id,
                Name           = s.Name,
                ContactPerson  = s.ContactPerson,
                Phone          = s.Phone,
                Email          = s.Email,
                Balance        = s.Balance,
                IsActive       = s.IsActive,
                OrderCount     = pt?.Count ?? 0,
                TotalPurchased = pt?.Total ?? 0m
            };
        }).ToList();

        ViewBag.Search = search;
        return View(items);
    }

    // ─── CREATE GET ──────────────────────────────────────────────────────────────
    public IActionResult Create() => View(new SupplierFormViewModel());

    // ─── CREATE POST ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupplierFormViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var supplier = new Supplier
        {
            Name          = vm.Name,
            ContactPerson = vm.ContactPerson,
            Phone         = vm.Phone,
            Email         = vm.Email,
            Address       = vm.Address,
            TaxNumber     = vm.TaxNumber,
            Notes         = vm.Notes,
            IsActive      = vm.IsActive
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Yeni tedarikçi: {Name}", supplier.Name);
        TempData["Success"] = $"'{supplier.Name}' tedarikçisi eklendi.";
        return RedirectToAction(nameof(Index));
    }

    // ─── EDIT GET ────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        var s = await _context.Suppliers.FindAsync(id);
        if (s is null) return NotFound();

        var vm = new SupplierFormViewModel
        {
            Id            = s.Id,
            Name          = s.Name,
            ContactPerson = s.ContactPerson,
            Phone         = s.Phone,
            Email         = s.Email,
            Address       = s.Address,
            TaxNumber     = s.TaxNumber,
            Notes         = s.Notes,
            IsActive      = s.IsActive
        };

        return View(vm);
    }

    // ─── EDIT POST ───────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SupplierFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        if (!ModelState.IsValid) return View(vm);

        var s = await _context.Suppliers.FindAsync(id);
        if (s is null) return NotFound();

        s.Name          = vm.Name;
        s.ContactPerson = vm.ContactPerson;
        s.Phone         = vm.Phone;
        s.Email         = vm.Email;
        s.Address       = vm.Address;
        s.TaxNumber     = vm.TaxNumber;
        s.Notes         = vm.Notes;
        s.IsActive      = vm.IsActive;

        await _context.SaveChangesAsync();
        TempData["Success"] = $"'{s.Name}' güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // ─── DELETE POST ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await _context.Suppliers.FindAsync(id);
        if (s is null) return NotFound();

        _context.Suppliers.Remove(s); // Soft delete via override
        await _context.SaveChangesAsync();

        TempData["Success"] = $"'{s.Name}' silindi.";
        return RedirectToAction(nameof(Index));
    }

    // ─── PURCHASE GET (Mal Kabul Formu) ─────────────────────────────────────────
    public async Task<IActionResult> Purchase()
    {
        var vm = new PurchaseViewModel
        {
            Suppliers  = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new SelectListItem(s.Name, s.Id.ToString()))
                .ToListAsync(),

            Warehouses = await _context.Warehouses
                .Where(w => w.IsActive)
                .OrderBy(w => w.Name)
                .Select(w => new SelectListItem(w.Name, w.Id.ToString()))
                .ToListAsync(),

            Products   = await _context.Products
                .OrderBy(p => p.Name)
                .Select(p => new SelectListItem($"{p.Name} ({p.SKU})", p.Id.ToString()))
                .ToListAsync()
        };

        return View(vm);
    }

    // ─── PURCHASE POST ────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purchase(PurchaseViewModel vm)
    {
        // Geçerli item satırlarını filtrele
        var validItems = vm.Items
            .Where(i => i.ProductId > 0 && i.Quantity > 0 && i.UnitCost > 0)
            .ToList();

        if (validItems.Count == 0)
        {
            TempData["Error"] = "En az bir ürün satırı doldurulmalıdır.";
            return RedirectToAction(nameof(Purchase));
        }

        var supplier = await _context.Suppliers.FindAsync(vm.SupplierId);
        if (supplier is null) return NotFound();

        var purchaseNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var userId         = _context.Users.First().Id; // Aktif kullanıcı

        var items = new List<PurchaseOrderItem>();
        decimal totalCost = 0;

        foreach (var row in validItems)
        {
            var product = await _context.Products.FindAsync(row.ProductId);
            if (product is null) continue;

            // Stoku artır
            product.StockCount += row.Quantity;

            items.Add(new PurchaseOrderItem
            {
                ProductId = row.ProductId,
                Quantity  = row.Quantity,
                UnitCost  = row.UnitCost
            });

            totalCost += row.Quantity * row.UnitCost;
        }

        // Tedarikçi bakiyesini güncelle (borç artar)
        supplier.Balance -= totalCost;

        var po = new PurchaseOrder
        {
            PurchaseNumber = purchaseNumber,
            SupplierId     = vm.SupplierId,
            WarehouseId    = vm.WarehouseId,
            Status         = PurchaseOrderStatus.Received,
            TotalCost      = totalCost,
            Notes          = vm.Notes,
            OrderedAt      = DateTime.UtcNow,
            ReceivedAt     = DateTime.UtcNow,
            Items          = items
        };

        _context.PurchaseOrders.Add(po);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Mal kabul: {PO}, Tedarikçi: {Supplier}, Toplam: {Total}",
            purchaseNumber, supplier.Name, totalCost);

        TempData["Success"] = $"Mal kabul tamamlandı ({purchaseNumber}). Stoklar güncellendi.";
        return RedirectToAction(nameof(Index));
    }
}
