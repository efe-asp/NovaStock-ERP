using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;

namespace NovaStock.Web.Controllers;

[Authorize]
public class NotificationController : Controller
{
    private readonly ApplicationDbContext _context;

    public NotificationController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Kullanıcının en son bildirimlerini getirir.
    /// Adminler genel bildirimleri, diğerleri kendilerine ait olanları görür (veya senaryoya göre değişir).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLatest()
    {
        var isAdmin = User.IsInRole("Admin");

        var query = _context.Notifications.AsQueryable();

        // Eğer adminse UserId'si null olanları da görsün (genel admin bildirimleri)
        if (isAdmin)
        {
            // Admin hem kendine aitleri, hem de genel olanları (UserId = null) görür
            query = query.Where(n => n.UserId == null); 
            // Note: İleride spesifik bir admine atanmışsa n.UserId == currentUserId şartı eklenebilir.
        }
        else
        {
            // Bayiler sadece kendilerine ait olanları görür.
            // Fakat mevcut Hub tasarımı sadece Admins grubuna bildirim atıyor.
            return Json(new { success = true, notifications = new List<object>(), unreadCount = 0 });
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.Type,
                n.IconClass,
                n.IsRead,
                TimeAgo = GetTimeAgo(n.CreatedAt)
            })
            .ToListAsync();

        var unreadCount = await query.CountAsync(n => !n.IsRead);

        return Json(new { success = true, notifications, unreadCount });
    }

    /// <summary>
    /// Tek bir bildirimi okundu olarak işaretler.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null) return NotFound();

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return Ok();
    }

    /// <summary>
    /// Tümünü okundu işaretler
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin) return Ok();

        var unreadNotifs = await _context.Notifications
            .Where(n => n.UserId == null && !n.IsRead)
            .ToListAsync();

        foreach(var notif in unreadNotifs)
        {
            notif.IsRead = true;
        }

        if(unreadNotifs.Any())
        {
            await _context.SaveChangesAsync();
        }

        return Ok();
    }

    private static string GetTimeAgo(DateTime dateTime)
    {
        var timeSpan = DateTime.UtcNow - dateTime;

        if (timeSpan <= TimeSpan.FromSeconds(60))
            return $"{timeSpan.Seconds} sn önce";
        if (timeSpan <= TimeSpan.FromMinutes(60))
            return $"{timeSpan.Minutes} dk önce";
        if (timeSpan <= TimeSpan.FromHours(24))
            return $"{timeSpan.Hours} sa önce";
        if (timeSpan <= TimeSpan.FromDays(30))
            return $"{timeSpan.Days} gün önce";

        return dateTime.ToString("dd.MM.yyyy");
    }
}
