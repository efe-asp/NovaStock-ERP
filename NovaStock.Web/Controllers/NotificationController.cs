using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.Models;

namespace NovaStock.Web.Controllers;

[Authorize]
public class NotificationController : Controller
{
    private readonly ApplicationDbContext         _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationController(
        ApplicationDbContext         context,
        UserManager<ApplicationUser> userManager)
    {
        _context     = context;
        _userManager = userManager;
    }

    /// <summary>
    /// Kullanıcının en son bildirimlerini getirir.
    /// - Admin: UserId == null olan genel bildirimleri görür.
    /// - Bayi / Alt kullanıcı: kendi UserId'siyle eşleşen bildirimleri görür.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLatest()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
            return Json(new { success = false });

        var isAdmin = User.IsInRole("Admin");

        IQueryable<Notification> query;

        if (isAdmin)
        {
            // Admin: UserId == null olan genel / admin bildirimleri
            query = _context.Notifications.Where(n => n.UserId == null);
        }
        else
        {
            // Bayi ve alt kullanıcılar: kendi bildirimlerini görür
            // Sub-user ise parent dealer'ın bildirimlerini de görebilsin
            var dealerId = currentUser.ParentDealerId ?? currentUser.Id;
            query = _context.Notifications
                .Where(n => n.UserId == currentUser.Id || n.UserId == dealerId);
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
    /// Tümünü okundu işaretler (hem admin hem bayi için).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Ok();

        var isAdmin = User.IsInRole("Admin");

        List<Notification> unreadNotifs;

        if (isAdmin)
        {
            unreadNotifs = await _context.Notifications
                .Where(n => n.UserId == null && !n.IsRead)
                .ToListAsync();
        }
        else
        {
            var dealerId = currentUser.ParentDealerId ?? currentUser.Id;
            unreadNotifs = await _context.Notifications
                .Where(n => (n.UserId == currentUser.Id || n.UserId == dealerId) && !n.IsRead)
                .ToListAsync();
        }

        foreach (var notif in unreadNotifs)
            notif.IsRead = true;

        if (unreadNotifs.Any())
            await _context.SaveChangesAsync();

        return Ok();
    }

    /// <summary>
    /// Admin + bayi için açık ticket sayısı ve okunmamış bildirim sayısı.
    /// Layout'ta rozet için JS tarafından fetch edilir.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCounts()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
            return Json(new { openTicketCount = 0 });

        var isAdmin = User.IsInRole("Admin");
        int openTicketCount;

        if (isAdmin)
        {
            openTicketCount = await _context.SupportTickets
                .CountAsync(t => t.Status == TicketStatus.Open);
        }
        else
        {
            var dealerId = currentUser.ParentDealerId ?? currentUser.Id;
            // Bayi için "yanıtlandı ama okunmadı" olan ticket sayısı
            openTicketCount = await _context.SupportTickets
                .Where(t => t.DealerId == dealerId && t.Status == TicketStatus.Answered)
                .CountAsync(t => t.Messages.Any(m => !m.IsRead && m.IsFromAdmin));
        }

        return Json(new { openTicketCount });
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
