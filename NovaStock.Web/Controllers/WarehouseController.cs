using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Depo yönetimi – Çoklu lokasyon stok takibi.
/// </summary>
[Authorize(Roles = "Admin")]
public class WarehouseController : Controller
{
    private readonly ApplicationDbContext _context;

    public WarehouseController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var warehouses = await _context.Warehouses
            .Include(w => w.ProductWarehouses)
            .ThenInclude(pw => pw.Product)
            .ToListAsync();

        return View(warehouses);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var warehouse = await _context.Warehouses
            .Include(w => w.ProductWarehouses)
            .ThenInclude(pw => pw.Product)
            .ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (warehouse is null) return NotFound();
        return View(warehouse);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transfer(int fromWarehouseId, int toWarehouseId, int productId, int quantity)
    {
        var from = await _context.ProductWarehouses
            .FirstOrDefaultAsync(pw => pw.WarehouseId == fromWarehouseId && pw.ProductId == productId);

        if (from is null || from.Quantity < quantity)
        {
            TempData["Error"] = "Kaynak depoda yeterli stok yok.";
            return RedirectToAction(nameof(Detail), new { id = fromWarehouseId });
        }

        var to = await _context.ProductWarehouses
            .FirstOrDefaultAsync(pw => pw.WarehouseId == toWarehouseId && pw.ProductId == productId);

        from.Quantity -= quantity;

        if (to is not null)
        {
            to.Quantity += quantity;
        }
        else
        {
            _context.ProductWarehouses.Add(new Models.ProductWarehouse
            {
                WarehouseId = toWarehouseId,
                ProductId   = productId,
                Quantity    = quantity
            });
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = $"{quantity} adet stok transferi başarıyla tamamlandı.";
        return RedirectToAction(nameof(Detail), new { id = fromWarehouseId });
    }
}

