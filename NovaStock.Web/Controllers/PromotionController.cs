using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.Models;
using NovaStock.Web.ViewModels;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Kampanya &amp; Fiyat Politikası Yönetimi. Sadece Admin erişebilir.
/// </summary>
[Authorize(Roles = "Admin")]
public class PromotionController : Controller
{
    private readonly ApplicationDbContext     _context;
    private readonly ILogger<PromotionController> _logger;

    public PromotionController(ApplicationDbContext context, ILogger<PromotionController> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ─── INDEX ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var promotions = await _context.Promotions
            .Include(p => p.Category)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(promotions);
    }

    // ─── CREATE GET ──────────────────────────────────────────────────────────────
    public async Task<IActionResult> Create()
    {
        var vm = new PromotionFormViewModel
        {
            Categories = await GetCategoryList()
        };
        return View(vm);
    }

    // ─── CREATE POST ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PromotionFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Categories = await GetCategoryList();
            return View(vm);
        }

        var promo = new Promotion
        {
            Name              = vm.Name,
            Type              = vm.Type,
            DiscountValue     = vm.DiscountValue,
            MinimumCartTotal  = vm.MinimumCartTotal,
            MinimumQuantity   = vm.MinimumQuantity,
            CategoryId        = vm.CategoryId,
            IsActive          = vm.IsActive,
            ValidUntil        = vm.ValidUntil
        };

        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Kampanya oluşturuldu: {Name}", promo.Name);
        TempData["Success"] = $"'{promo.Name}' kampanyası oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    // ─── EDIT GET ────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        var p = await _context.Promotions.FindAsync(id);
        if (p is null) return NotFound();

        var vm = new PromotionFormViewModel
        {
            Id               = p.Id,
            Name             = p.Name,
            Type             = p.Type,
            DiscountValue    = p.DiscountValue,
            MinimumCartTotal = p.MinimumCartTotal,
            MinimumQuantity  = p.MinimumQuantity,
            CategoryId       = p.CategoryId,
            IsActive         = p.IsActive,
            ValidUntil       = p.ValidUntil,
            Categories       = await GetCategoryList()
        };

        return View(vm);
    }

    // ─── EDIT POST ───────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PromotionFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            vm.Categories = await GetCategoryList();
            return View(vm);
        }

        var p = await _context.Promotions.FindAsync(id);
        if (p is null) return NotFound();

        p.Name             = vm.Name;
        p.Type             = vm.Type;
        p.DiscountValue    = vm.DiscountValue;
        p.MinimumCartTotal = vm.MinimumCartTotal;
        p.MinimumQuantity  = vm.MinimumQuantity;
        p.CategoryId       = vm.CategoryId;
        p.IsActive         = vm.IsActive;
        p.ValidUntil       = vm.ValidUntil;

        await _context.SaveChangesAsync();
        TempData["Success"] = $"'{p.Name}' güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // ─── TOGGLE POST (Aktif / Pasif) ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var p = await _context.Promotions.FindAsync(id);
        if (p is null) return NotFound();

        p.IsActive = !p.IsActive;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"'{p.Name}' {(p.IsActive ? "aktif" : "pasif")} edildi.";
        return RedirectToAction(nameof(Index));
    }

    // ─── DELETE POST ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _context.Promotions.FindAsync(id);
        if (p is null) return NotFound();

        _context.Promotions.Remove(p);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"'{p.Name}' silindi.";
        return RedirectToAction(nameof(Index));
    }

    // ─── Helper ───────────────────────────────────────────────────────────────────
    private async Task<List<SelectListItem>> GetCategoryList()
        => await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();
}

