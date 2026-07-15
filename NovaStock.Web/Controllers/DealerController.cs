using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.Models;
using NovaStock.Web.Services;
using NovaStock.Web.ViewModels;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Bayi Yönetimi – Admin: Tüm bayileri listele, düzenle.
/// Dealer: Kendi profili, cari ekstre, sub-user yönetimi.
/// </summary>
[Authorize]
public class DealerController : Controller
{
    private readonly ApplicationDbContext         _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DealerController>    _logger;
    private readonly ExcelService                 _excelService;
    private readonly PdfService                   _pdfService;

    public DealerController(
        ApplicationDbContext          context,
        UserManager<ApplicationUser>  userManager,
        ILogger<DealerController>     logger,
        ExcelService                  excelService,
        PdfService                    pdfService)
    {
        _context      = context;
        _userManager  = userManager;
        _logger       = logger;
        _excelService = excelService;
        _pdfService   = pdfService;
    }

    // ─── INDEX ──────────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index(string? search, string? tier)
    {
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
    [HttpPost, ValidateAntiForgeryToken]
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

    // ─── STATEMENT – Gelişmiş Cari Hesap Ekstresi ────────────────────────────────
    [Authorize(Roles = "Dealer,DealerFinance")]
    public async Task<IActionResult> Statement()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        var dealerId = currentUser.ParentDealerId ?? currentUser.Id;
        var dealer   = dealerId != currentUser.Id
            ? await _userManager.FindByIdAsync(dealerId) ?? currentUser
            : currentUser;

        var vm = await BuildStatementViewModel(dealer);
        return View(vm);
    }

    // ─── STATEMENT PDF İNDİR ─────────────────────────────────────────────────────
    [Authorize(Roles = "Dealer,DealerFinance")]
    public async Task<IActionResult> ExportStatementPdf()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        var dealerId = currentUser.ParentDealerId ?? currentUser.Id;
        var dealer   = dealerId != currentUser.Id
            ? await _userManager.FindByIdAsync(dealerId) ?? currentUser
            : currentUser;

        var vm  = await BuildStatementViewModel(dealer);
        var pdf = _pdfService.GenerateStatementPdf(vm);

        var fileName = $"CariEkstre_{dealer.CompanyName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    // ─── STATEMENT EXCEL İNDİR ───────────────────────────────────────────────────
    [Authorize(Roles = "Dealer,DealerFinance")]
    public async Task<IActionResult> ExportStatementExcel()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        var dealerId = currentUser.ParentDealerId ?? currentUser.Id;
        var dealer   = dealerId != currentUser.Id
            ? await _userManager.FindByIdAsync(dealerId) ?? currentUser
            : currentUser;

        var vm    = await BuildStatementViewModel(dealer);
        var bytes = _excelService.ExportStatementExcel(
            vm.CompanyName, vm.DealerName, vm.Entries, vm.TotalDebt, vm.CreditLimit);

        var fileName = $"CariEkstre_{dealer.CompanyName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // ─── SANAL POS – Ödeme Modal Handler ─────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Dealer")]
    public async Task<IActionResult> PayWithCard(decimal amount, string cardHolder,
        string cardNumber, string expiry, string cvv)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var dealerId = currentUser.ParentDealerId ?? currentUser.Id;

        _logger.LogInformation("Sanal POS ödeme talebi: Bayi={Dealer}, Tutar={Amount}", dealerId, amount);

        var docNumber = $"TX-{DateTime.Now:yyMMddHHmm}";
        var lastEntry = await _context.LedgerEntries
            .Where(e => e.DealerId == dealerId)
            .OrderByDescending(e => e.TransactionDate)
            .FirstOrDefaultAsync();

        var currentBalance = lastEntry?.RunningBalance ?? 0m;
        var newBalance     = currentBalance - amount;

        var entry = new LedgerEntry
        {
            DealerId        = dealerId,
            TransactionType = LedgerTransactionType.VirtualPos,
            DocumentNumber  = docNumber,
            Credit          = amount,
            Debit           = 0m,
            RunningBalance  = newBalance,
            Description     = $"Web Sanal POS Tahsilatı – Kart Son 4: {(cardNumber.Length >= 4 ? cardNumber[^4..] : "****")}",
            TransactionDate = DateTime.UtcNow
        };

        _context.LedgerEntries.Add(entry);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"✅ {amount:N2} ₺ tutarındaki ödemeniz başarıyla alındı. Evrak No: {docNumber}";
        return RedirectToAction(nameof(Statement));
    }

    // ─── EKİP YÖNETİMİ – Sub-User Listesi ───────────────────────────────────────
    [Authorize(Roles = "Dealer")]
    public async Task<IActionResult> TeamManagement()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        if (currentUser.ParentDealerId != null)
        {
            TempData["Error"] = "Alt kullanıcılar ekip yönetemez.";
            return RedirectToAction(nameof(Statement));
        }

        var subUsers = _userManager.Users
            .Where(u => u.ParentDealerId == currentUser.Id)
            .ToList();

        var vms = new List<SubUserViewModel>();
        foreach (var u in subUsers)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var role  = roles.FirstOrDefault() ?? "DealerPurchase";
            vms.Add(new SubUserViewModel
            {
                Id        = u.Id,
                FullName  = u.FullName,
                Email     = u.Email ?? "",
                Role      = role,
                RoleLabel = role == "DealerFinance" ? "Muhasebe Personeli" : "Satın Alma Personeli",
                IsActive  = u.IsActive,
                CreatedAt = u.CreatedAt
            });
        }

        ViewBag.DealerName = currentUser.FullName;
        return View(vms);
    }

    // ─── EKİP – Alt Kullanıcı Ekle ───────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Dealer")]
    public async Task<IActionResult> AddSubUser(CreateSubUserViewModel vm)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        if (currentUser.ParentDealerId != null)
        {
            TempData["Error"] = "Alt kullanıcılar başka alt kullanıcı ekleyemez.";
            return RedirectToAction(nameof(TeamManagement));
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Form bilgileri eksik veya hatalı.";
            return RedirectToAction(nameof(TeamManagement));
        }

        var newUser = new ApplicationUser
        {
            UserName        = vm.Email,
            Email           = vm.Email,
            FullName        = vm.FullName,
            CompanyName     = currentUser.CompanyName,
            Tier            = currentUser.Tier,
            IsActive        = true,
            EmailConfirmed  = true,
            ParentDealerId  = currentUser.Id
        };

        var result = await _userManager.CreateAsync(newUser, vm.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(newUser, vm.Role);
            _logger.LogInformation("Alt kullanıcı eklendi: {Email} → {Role} (Ana Bayi: {Parent})",
                vm.Email, vm.Role, currentUser.FullName);
            TempData["Success"] = $"'{vm.FullName}' ekibinize eklendi.";
        }
        else
        {
            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(TeamManagement));
    }

    // ─── EKİP – Alt Kullanıcı Devre Dışı ────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Dealer")]
    public async Task<IActionResult> RemoveSubUser(string subUserId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        var subUser = await _userManager.FindByIdAsync(subUserId);
        if (subUser is null || subUser.ParentDealerId != currentUser.Id)
        {
            TempData["Error"] = "Yetkisiz işlem.";
            return RedirectToAction(nameof(TeamManagement));
        }

        subUser.IsActive = false;
        await _userManager.UpdateAsync(subUser);

        TempData["Success"] = $"'{subUser.FullName}' hesabı devre dışı bırakıldı.";
        return RedirectToAction(nameof(TeamManagement));
    }

    // ─── YARDIMCI: StatementViewModel oluştur ────────────────────────────────────
    private async Task<StatementViewModel> BuildStatementViewModel(ApplicationUser dealer)
    {
        var dealerId = dealer.Id;

        var entries = await _context.LedgerEntries
            .Where(e => e.DealerId == dealerId)
            .OrderByDescending(e => e.TransactionDate)
            .ToListAsync();

        if (!entries.Any())
        {
            var orders = await _context.Orders
                .Where(o => o.DealerId == dealerId && o.Status == OrderStatus.Delivered)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            decimal running = 0m;
            foreach (var o in orders)
            {
                running += o.Total;
                entries.Add(new LedgerEntry
                {
                    DealerId        = dealerId,
                    TransactionType = LedgerTransactionType.Invoice,
                    DocumentNumber  = $"FAT-{o.OrderNumber}",
                    Debit           = o.Total,
                    Credit          = 0m,
                    RunningBalance  = running,
                    Description     = $"Sipariş Faturası: {o.OrderNumber}",
                    TransactionDate = o.CreatedAt,
                    DueDate         = o.CreatedAt.AddDays(30),
                    RelatedOrderId  = o.Id
                });
            }
        }

        var totalDebit  = entries.Sum(e => e.Debit);
        var totalCredit = entries.Sum(e => e.Credit);
        var netBalance  = totalDebit - totalCredit;
        var overdueDebt = entries
            .Where(e => e.DueDate.HasValue && e.DueDate.Value < DateTime.UtcNow && e.Debit > 0)
            .Sum(e => e.Debit);

        var creditLimit = dealer.CreditLimitOverride ?? dealer.Tier switch
        {
            DealerTier.Gold   => 500_000m,
            DealerTier.Silver => 200_000m,
            _                 => 50_000m
        };

        var usedPct = creditLimit > 0 ? Math.Min((double)netBalance / (double)creditLimit * 100, 100) : 0;

        static (string label, string icon, string badge) GetTypeInfo(LedgerTransactionType t) => t switch
        {
            LedgerTransactionType.Invoice      => ("Fatura",         "fa-file-invoice",      "badge-red"),
            LedgerTransactionType.Payment      => ("Ödeme/Tahsilat", "fa-money-bill-wave",    "badge-green"),
            LedgerTransactionType.Refund       => ("İade",           "fa-rotate-left",        "badge-cyan"),
            LedgerTransactionType.BankTransfer => ("Havale/EFT",     "fa-building-columns",   "badge-blue"),
            LedgerTransactionType.VirtualPos   => ("Sanal POS",      "fa-credit-card",        "badge-purple"),
            LedgerTransactionType.Adjustment   => ("Düzeltme",       "fa-sliders",            "badge-gray"),
            _                                  => ("Diğer",          "fa-circle",             "badge-gray")
        };

        var entryVms = entries.Select(e =>
        {
            var (label, icon, badge) = GetTypeInfo(e.TransactionType);
            return new LedgerEntryViewModel
            {
                Id                   = e.Id,
                DocumentNumber       = e.DocumentNumber,
                Debit                = e.Debit,
                Credit               = e.Credit,
                RunningBalance       = e.RunningBalance,
                Description          = e.Description,
                TransactionDate      = e.TransactionDate,
                DueDate              = e.DueDate,
                RelatedOrderId       = e.RelatedOrderId,
                TransactionTypeLabel = label,
                TransactionTypeIcon  = icon,
                TransactionTypeBadge = badge
            };
        }).ToList();

        return new StatementViewModel
        {
            DealerName   = dealer.FullName,
            CompanyName  = dealer.CompanyName,
            Tier         = dealer.Tier.ToString(),
            TotalDebt    = totalDebit,
            TotalCredit  = totalCredit,
            NetBalance   = netBalance,
            CreditLimit  = creditLimit,
            OverdueDebt  = overdueDebt,
            UsedLimitPct = (decimal)usedPct,
            Entries      = entryVms
        };
    }
}
