using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Denetim Günlüğü – Tüm veri değişikliklerini listeler.
/// Sadece Admin erişebilir.
/// </summary>
[Authorize(Roles = "Admin")]
public class AuditController : Controller
{
    private readonly ApplicationDbContext _context;

    public AuditController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(
        string? search,
        string? tableName,
        string? action,
        DateTime? from,
        DateTime? to,
        int page = 1)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(l => l.UserName!.Contains(search) || l.Description!.Contains(search));

        if (!string.IsNullOrWhiteSpace(tableName))
            query = query.Where(l => l.TableName == tableName);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action == action);

        if (from.HasValue)
            query = query.Where(l => l.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.Timestamp <= to.Value.AddDays(1));

        const int pageSize = 30;
        var total = await query.CountAsync();
        var logs  = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Total      = total;
        ViewBag.Page       = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.Search     = search;
        ViewBag.TableNames = await _context.AuditLogs.Select(l => l.TableName).Distinct().ToListAsync();

        return View(logs);
    }
}
