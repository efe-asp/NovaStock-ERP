using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.ViewModels;
using IOFile = System.IO.File;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Sistem Ayarları – SMTP, kritik stok eşiği ve sistem bilgileri.
/// </summary>
[Authorize(Roles = "Admin")]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration       _config;
    private readonly IWebHostEnvironment  _env;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ApplicationDbContext      context,
        IConfiguration            config,
        IWebHostEnvironment       env,
        ILogger<SettingsController> logger)
    {
        _context = context;
        _config  = config;
        _env     = env;
        _logger  = logger;
    }

    // ─── INDEX ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var dbPath = Path.Combine(_env.ContentRootPath, "novastock.db");
        var dbSize = IOFile.Exists(dbPath) ? new FileInfo(dbPath).Length : 0L;

        var vm = new SettingsViewModel
        {
            // SMTP – appsettings.json'dan oku
            SmtpHost     = _config["Email:SmtpHost"]     ?? string.Empty,
            SmtpPort     = _config["Email:SmtpPort"]     ?? "587",
            SmtpUsername = _config["Email:Username"]     ?? string.Empty,
            SmtpFromName = _config["Email:FromName"]     ?? string.Empty,
            AdminEmail   = _config["Email:AdminEmail"]   ?? string.Empty,

            // Sistem bilgileri
            AppVersion   = "1.0.0",
            DatabasePath = dbPath,
            DbSizeBytes  = dbSize,

            // İstatistikler
            TotalProducts  = await _context.Products.CountAsync(),
            TotalOrders    = await _context.Orders.CountAsync(),
            TotalUsers     = await _context.Users.CountAsync(),
            TotalAuditLogs = await _context.AuditLogs.CountAsync()
        };

        return View(vm);
    }

    // ─── SAVE POST ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Not: Gerçek bir üretim ortamında appsettings.json'u doğrudan yazmak yerine
    /// bu değerler veritabanında tutulmalıdır. Bu implementasyon demonstrasyon amaçlıdır.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SettingsViewModel vm)
    {
        // appsettings.json'u güncelle (runtime ayar değişikliği)
        var appsettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");

        if (IOFile.Exists(appsettingsPath))
        {
            var json = await IOFile.ReadAllTextAsync(appsettingsPath);
            var jsonObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(json);

            if (jsonObj != null)
            {
                if (jsonObj["Email"] is System.Text.Json.Nodes.JsonObject emailSection)
                {
                    emailSection["SmtpHost"]   = vm.SmtpHost;
                    emailSection["SmtpPort"]   = vm.SmtpPort;
                    emailSection["Username"]   = vm.SmtpUsername;
                    emailSection["FromName"]   = vm.SmtpFromName;
                    emailSection["AdminEmail"] = vm.AdminEmail;
                }

                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                await IOFile.WriteAllTextAsync(
                    appsettingsPath,
                    System.Text.Json.JsonSerializer.Serialize(jsonObj, options));
            }
        }

        _logger.LogInformation("Sistem ayarları güncellendi.");
        TempData["Success"] = "Ayarlar kaydedildi. Değişikliklerin tam etkisi için uygulamayı yeniden başlatın.";
        return RedirectToAction(nameof(Index));
    }

    // ─── DB TEMIZLE (Audit Log) ───────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAuditLogs()
    {
        var cutoff  = DateTime.UtcNow.AddDays(-90); // 90 günden eski logları sil
        var oldLogs = _context.AuditLogs.Where(l => l.Timestamp < cutoff);
        _context.AuditLogs.RemoveRange(oldLogs);
        var count = await _context.SaveChangesAsync();

        _logger.LogWarning("Audit log temizlendi: {Count} kayıt silindi.", count);
        TempData["Success"] = $"{count} eski audit log kaydı silindi.";
        return RedirectToAction(nameof(Index));
    }
}

