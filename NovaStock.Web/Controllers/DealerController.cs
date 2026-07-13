using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.Models;
using NovaStock.Web.ViewModels;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Bayi Yönetimi – Admin: Tüm bayileri listele, düzenle. Dealer: Kendi profilini gör.
/// </summary>
[Authorize]
public class DealerController : Controller
{
    private readonly ApplicationDbContext         _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DealerController>    _logger;

    public DealerController(
        ApplicationDbContext          context,
        UserManager<ApplicationUser>  userManager,
        ILogger<DealerController>     logger)
    {
        _context     = context;
        _userManager = userManager;
        _logger      = logger;
    }

    // ─── INDEX ──────────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index(string? search, string? tier)
    {
        // Tüm siparişleri de çek; sonra in-memory grupla
        var orders = await _context.Orders
            .GroupBy(o => o.DealerId)
            .Select(g => new
            {
                DealerId   = g.Key,
                Count      = g.Count(),
                TotalSpent = g.Sum(o => o.Total)
            })
            .ToListAsync();

        var usersQuery = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            usersQuery = usersQuery.Where(u =>
                u.FullName.Contains(search) ||
                u.CompanyName.Contains(search) ||
                u.Email!.Contains(search));

        if (!string.IsNullOrWhiteSpace(tier) && Enum.TryParse<DealerTier>(tier, out var tierEnum))
            usersQuery = usersQuery.Where(u => u.Tier == tierEnum);

        var users = await usersQuery
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var items = users.Select(u =>
        {
            var ord = orders.FirstOrDefault(o => o.DealerId == u.Id);
            return new DealerListItem
            {
                Id          = u.Id,
                FullName    = u.FullName,
                CompanyName = u.CompanyName,
                Email       = u.Email ?? "",
                Phone       = u.PhoneNumber,
                Tier        = u.Tier,
                IsActive    = u.IsActive,
                OrderCount  = ord?.Count ?? 0,
                TotalSpent  = ord?.TotalSpent ?? 0m,
                CreatedAt   = u.CreatedAt
            };
        }).ToList();

        ViewBag.Search    = search;
        ViewBag.TierFilter = tier;
        return View(items);
    }

    // ─── DETAIL ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Detail(string id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var isAdmin     = User.IsInRole("Admin");

        // Bayi sadece kendi profilini görebilir
        if (!isAdmin && currentUser?.Id != id) return Forbid();

        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var orders = await _context.Orders
            .Where(o => o.DealerId == id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .Select(o => new DealerOrderItem
            {
                Id          = o.Id,
                OrderNumber = o.OrderNumber,
                Total       = o.Total,
                Status      = o.Status.ToString(),
                CreatedAt   = o.CreatedAt
            })
            .ToListAsync();

        var totalSpent = await _context.Orders
            .Where(o => o.DealerId == id && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;

        var pendingAmount = await _context.Orders
            .Where(o => o.DealerId == id && o.Status == OrderStatus.Pending)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;

        var vm = new DealerDetailViewModel
        {
            Id            = user.Id,
            FullName      = user.FullName,
            CompanyName   = user.CompanyName,
            Email         = user.Email ?? "",
            Phone         = user.PhoneNumber,
            Address       = user.Address,
            TaxNumber     = user.TaxNumber,
            Tier          = user.Tier,
            IsActive      = user.IsActive,
            CreatedAt     = user.CreatedAt,
            OrderCount    = orders.Count,
            TotalSpent    = totalSpent,
            PendingAmount = pendingAmount,
            RecentOrders  = orders
        };

        return View(vm);
    }

    // ─── EDIT GET ────────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var vm = new DealerEditViewModel
        {
            Id       = user.Id,
            Tier     = user.Tier,
            IsActive = user.IsActive
        };

        ViewBag.DealerName = user.FullName;
        return View(vm);
    }

    // ─── EDIT POST ───────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DealerEditViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _userManager.FindByIdAsync(vm.Id);
        if (user is null) return NotFound();

        user.Tier     = vm.Tier;
        user.IsActive = vm.IsActive;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            _logger.LogInformation("Bayi güncellendi: {User} → Tier: {Tier}, Aktif: {Active}",
                user.FullName, vm.Tier, vm.IsActive);
            TempData["Success"] = $"'{user.FullName}' bayisi güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var e in result.Errors)
            ModelState.AddModelError("", e.Description);

        return View(vm);
    }
    // ─── STATEMENT – Cari Hesap Ekstresi ────────────────────────────────────────
    [Authorize(Roles = "Bayi")]
    public async Task<IActionResult> Statement()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        var dealerId = currentUser.Id;

        var orders = await _context.Orders
            .Where(o => o.DealerId == dealerId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new DealerStatementItem
            {
                Id          = o.Id,
                OrderNumber = o.OrderNumber,
                Total       = o.Total,
                Status      = o.Status.ToString(),
                CreatedAt   = o.CreatedAt
            })
            .ToListAsync();

        var totalDebt = await _context.Orders
            .Where(o => o.DealerId == dealerId && o.Status == OrderStatus.Delivered)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;

        var creditLimit = currentUser.Tier switch
        {
            DealerTier.Gold   => 500_000m,
            DealerTier.Silver => 200_000m,
            _                 => 50_000m
        };

        ViewBag.DealerName    = currentUser.FullName;
        ViewBag.CompanyName   = currentUser.CompanyName;
        ViewBag.Tier          = currentUser.Tier.ToString();
        ViewBag.TotalDebt     = totalDebt;
        ViewBag.CreditLimit   = creditLimit;
        ViewBag.Remaining     = creditLimit - totalDebt;

        return View(orders);
    }
}
