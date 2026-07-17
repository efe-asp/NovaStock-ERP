using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.Hubs;
using NovaStock.Web.Models;
using NovaStock.Web.ViewModels;
using System.Text.Json;

namespace NovaStock.Web.Controllers;

/// <summary>
/// B2B Destek Talepleri (Support Ticket / RMA Portal).
/// Bayiler destek talebi oluşturur, admin yanıtlar.
/// Her iki taraf da bildirim alır (DB + SignalR).
/// </summary>
[Authorize]
public class SupportController : Controller
{
    private readonly ApplicationDbContext         _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SupportController>   _logger;
    private readonly IWebHostEnvironment          _env;
    private readonly IHubContext<NotificationHub> _hub;

    public SupportController(
        ApplicationDbContext          context,
        UserManager<ApplicationUser>  userManager,
        ILogger<SupportController>    logger,
        IWebHostEnvironment           env,
        IHubContext<NotificationHub>  hub)
    {
        _context     = context;
        _userManager = userManager;
        _logger      = logger;
        _env         = env;
        _hub         = hub;
    }

    // ─── INDEX – Talep Listesi ───────────────────────────────────────────────────
    public async Task<IActionResult> Index(string? filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        var isAdmin = User.IsInRole("Admin");

        // Sub-user ise ana bayinin ID'sini kullan
        var dealerId = currentUser.ParentDealerId ?? currentUser.Id;

        var query = _context.SupportTickets
            .Include(t => t.AssignedTo)
            .Include(t => t.Messages)
            .AsQueryable();

        // Admin tüm talepleri görür, bayi sadece kendi taleplerini
        if (!isAdmin)
            query = query.Where(t => t.DealerId == dealerId);

        // Filtre
        query = filter switch
        {
            "open"     => query.Where(t => t.Status == TicketStatus.Open),
            "answered" => query.Where(t => t.Status == TicketStatus.Answered),
            "resolved" => query.Where(t => t.Status == TicketStatus.Resolved),
            _          => query
        };

        var tickets = await query
            .OrderByDescending(t => t.LastActivityAt)
            .ToListAsync();

        var vms = tickets.Select(t => new SupportTicketListItem
        {
            Id                 = t.Id,
            Subject            = t.Subject,
            Category           = t.Category,
            Priority           = t.Priority,
            Status             = t.Status,
            RelatedOrderNumber = t.RelatedOrderNumber,
            AssignedToName     = t.AssignedTo?.FullName,
            CreatedAt          = t.CreatedAt,
            LastActivityAt     = t.LastActivityAt,
            UnreadCount        = t.Messages.Count(m => !m.IsRead && m.IsFromAdmin != isAdmin)
        }).ToList();

        ViewBag.Filter  = filter ?? "all";
        ViewBag.IsAdmin = isAdmin;
        ViewBag.OpenCount     = tickets.Count(t => t.Status == TicketStatus.Open);
        ViewBag.AnsweredCount = tickets.Count(t => t.Status == TicketStatus.Answered);
        ViewBag.ResolvedCount = tickets.Count(t => t.Status == TicketStatus.Resolved);

        return View(vms);
    }

    // ─── DETAIL – Talep Detayı & Mesajlaşma ─────────────────────────────────────
    public async Task<IActionResult> Detail(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        var isAdmin  = User.IsInRole("Admin");
        var dealerId = currentUser.ParentDealerId ?? currentUser.Id;

        var ticket = await _context.SupportTickets
            .Include(t => t.Dealer)
            .Include(t => t.AssignedTo)
            .Include(t => t.RelatedOrder)
            .Include(t => t.Messages)
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null) return NotFound();

        // Bayi sadece kendi taleplerini görebilir
        if (!isAdmin && ticket.DealerId != dealerId) return Forbid();

        // Okunmamış mesajları okundu işaretle
        var unread = ticket.Messages
            .Where(m => !m.IsRead && m.IsFromAdmin != isAdmin)
            .ToList();

        foreach (var m in unread)
            m.IsRead = true;

        if (unread.Any())
            await _context.SaveChangesAsync();

        var messageVms = ticket.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m =>
            {
                var attachments = new List<AttachmentItem>();
                if (!string.IsNullOrEmpty(m.AttachmentsJson))
                {
                    try
                    {
                        attachments = JsonSerializer.Deserialize<List<AttachmentItem>>(m.AttachmentsJson) ?? [];
                    }
                    catch { /* ignore */ }
                }

                return new TicketMessageViewModel
                {
                    Id          = m.Id,
                    SenderName  = m.Sender?.FullName ?? "Sistem",
                    Body        = m.Body,
                    IsFromAdmin = m.IsFromAdmin,
                    CreatedAt   = m.CreatedAt,
                    Attachments = attachments
                };
            })
            .ToList();

        // Admin kullanıcı listesini getir (temsilci atama dropdown'ı için)
        var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
        var adminSelectItems = adminUsers
            .OrderBy(u => u.FullName)
            .Select(u => new AdminSelectItem { Id = u.Id, FullName = u.FullName })
            .ToList();

        var vm = new SupportTicketDetailViewModel
        {
            Id                 = ticket.Id,
            Subject            = ticket.Subject,
            Category           = ticket.Category,
            Priority           = ticket.Priority,
            Status             = ticket.Status,
            RelatedOrderNumber = ticket.RelatedOrderNumber,
            RelatedOrderId     = ticket.RelatedOrderId,
            AssignedToId       = ticket.AssignedToId,
            AssignedToName     = ticket.AssignedTo?.FullName,
            DealerName         = ticket.Dealer?.FullName ?? "",
            DealerCompany      = ticket.Dealer?.CompanyName ?? "",
            CreatedAt          = ticket.CreatedAt,
            LastActivityAt     = ticket.LastActivityAt,
            IsAdmin            = isAdmin,
            AdminUsers         = adminSelectItems,
            Messages           = messageVms
        };

        return View(vm);
    }

    // ─── CREATE GET ──────────────────────────────────────────────────────────────
    [Authorize(Roles = "Dealer,DealerPurchase,DealerFinance")]
    public IActionResult Create()
    {
        return View(new CreateTicketViewModel());
    }

    // ─── CREATE POST ─────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Dealer,DealerPurchase,DealerFinance")]
    public async Task<IActionResult> Create(CreateTicketViewModel vm)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        if (!ModelState.IsValid) return View(vm);

        var dealerId = currentUser.ParentDealerId ?? currentUser.Id;

        var ticket = new SupportTicket
        {
            DealerId           = dealerId,
            Subject            = vm.Subject,
            Category           = vm.Category,
            Priority           = vm.Priority,
            Status             = TicketStatus.Open,
            RelatedOrderNumber = vm.RelatedOrderNumber,
            LastActivityAt     = DateTime.UtcNow
        };

        // İlgili sipariş varsa ID'sini bul
        if (!string.IsNullOrEmpty(vm.RelatedOrderNumber))
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderNumber == vm.RelatedOrderNumber && o.DealerId == dealerId);
            ticket.RelatedOrderId = order?.Id;
        }

        _context.SupportTickets.Add(ticket);
        await _context.SaveChangesAsync();

        // İlk mesajı ekle
        var attachmentsJson = await SaveAttachmentAsync(vm.Attachment);

        var message = new TicketMessage
        {
            SupportTicketId = ticket.Id,
            SenderId        = currentUser.Id,
            Body            = vm.Message,
            IsFromAdmin     = false,
            AttachmentsJson = attachmentsJson
        };

        _context.TicketMessages.Add(message);

        // ── Adminlere DB bildirimi oluştur ────────────────────────────────────────
        var adminNotification = new Notification
        {
            Title     = $"Yeni Destek Talebi – #DST-{ticket.Id:D4}",
            Message   = $"{currentUser.FullName} ({currentUser.CompanyName}): {vm.Subject}",
            Type      = "support",
            IconClass = "fa-headset",
            UserId    = null  // null = tüm adminler
        };
        _context.Notifications.Add(adminNotification);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Yeni destek talebi: #{Id} – {Subject} ({Category}) – Bayi: {Dealer}",
            ticket.Id, ticket.Subject, ticket.Category, currentUser.FullName);

        // ── Adminlere SignalR gerçek zamanlı bildirim ─────────────────────────────
        await _hub.Clients.Group("Admins").SendAsync("ReceiveSupportTicketNotification", new
        {
            TicketId   = ticket.Id,
            Subject    = ticket.Subject,
            DealerName = currentUser.FullName,
            Timestamp  = DateTime.Now.ToString("HH:mm")
        });

        TempData["Success"] = $"Destek talebiniz oluşturuldu. Talep No: #DST-{ticket.Id:D4}";
        return RedirectToAction(nameof(Detail), new { id = ticket.Id });
    }

    // ─── REPLY POST ──────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(ReplyViewModel vm)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Yanıt mesajı boş olamaz.";
            return RedirectToAction(nameof(Detail), new { id = vm.TicketId });
        }

        var isAdmin = User.IsInRole("Admin");
        var dealerId = currentUser.ParentDealerId ?? currentUser.Id;

        var ticket = await _context.SupportTickets
            .Include(t => t.Dealer)
            .FirstOrDefaultAsync(t => t.Id == vm.TicketId);
        if (ticket is null) return NotFound();

        if (!isAdmin && ticket.DealerId != dealerId) return Forbid();

        var attachmentsJson = await SaveAttachmentAsync(vm.Attachment);

        var message = new TicketMessage
        {
            SupportTicketId = vm.TicketId,
            SenderId        = currentUser.Id,
            Body            = vm.Body,
            IsFromAdmin     = isAdmin,
            AttachmentsJson = attachmentsJson
        };

        _context.TicketMessages.Add(message);

        // Durum güncelle
        ticket.LastActivityAt = DateTime.UtcNow;
        ticket.Status = isAdmin ? TicketStatus.Answered : TicketStatus.Open;

        if (vm.CloseTicket && isAdmin)
        {
            ticket.Status   = TicketStatus.Resolved;
            ticket.ClosedAt = DateTime.UtcNow;
        }

        // Admin atama
        if (isAdmin && ticket.AssignedToId == null)
            ticket.AssignedToId = currentUser.Id;

        if (isAdmin)
        {
            // ── Admin yanıtladı → Bayiye bildirim ────────────────────────────────
            var statusText = vm.CloseTicket ? "kapatıldı" : "yanıtlandı";
            var dealerNotification = new Notification
            {
                Title     = $"Destek Talebiniz {char.ToUpper(statusText[0])}{statusText[1..]} – #DST-{ticket.Id:D4}",
                Message   = $"{ticket.Subject} başlıklı talebiniz {statusText}.",
                Type      = "support",
                IconClass = "fa-headset",
                UserId    = ticket.DealerId  // bayiye özel
            };
            _context.Notifications.Add(dealerNotification);
            await _context.SaveChangesAsync();

            // SignalR: bayi kişisel grubuna gönder
            await _hub.Clients.Group($"Dealer_{ticket.DealerId}").SendAsync("ReceiveDealerNotification", new
            {
                TicketId  = ticket.Id,
                Message   = $"#{ticket.Id:D4} no'lu talebiniz {statusText}.",
                Timestamp = DateTime.Now.ToString("HH:mm")
            });
        }
        else
        {
            // ── Bayi yanıtladı → Adminlere bildirim ──────────────────────────────
            var adminNotification = new Notification
            {
                Title     = $"Destek Talebinde Yeni Mesaj – #DST-{ticket.Id:D4}",
                Message   = $"{ticket.Dealer?.FullName ?? "Bayi"}: {ticket.Subject}",
                Type      = "support",
                IconClass = "fa-headset",
                UserId    = null  // null = tüm adminler
            };
            _context.Notifications.Add(adminNotification);
            await _context.SaveChangesAsync();

            // SignalR: admin grubuna gönder
            await _hub.Clients.Group("Admins").SendAsync("ReceiveSupportTicketNotification", new
            {
                TicketId   = ticket.Id,
                Subject    = ticket.Subject,
                DealerName = ticket.Dealer?.FullName ?? "Bayi",
                Timestamp  = DateTime.Now.ToString("HH:mm")
            });
        }

        return RedirectToAction(nameof(Detail), new { id = vm.TicketId });
    }

    // ─── ASSIGN POST (Admin) ────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Assign(int ticketId, string assignedToId)
    {
        var ticket = await _context.SupportTickets
            .Include(t => t.Dealer)
            .FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket is null) return NotFound();

        // Atanan temsilciyi güncelle
        var assignedAdmin = await _userManager.FindByIdAsync(assignedToId);
        if (assignedAdmin is null)
        {
            TempData["Error"] = "Seçilen temsilci bulunamadı.";
            return RedirectToAction(nameof(Detail), new { id = ticketId });
        }

        ticket.AssignedToId   = assignedToId;
        ticket.LastActivityAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Bayiye bildirim: temsilci atandı
        var dealerNotification = new Notification
        {
            Title     = $"Temsilci Atandı – #DST-{ticketId:D4}",
            Message   = $"{ticket.Subject} talebinize {assignedAdmin.FullName} atandı.",
            Type      = "support",
            IconClass = "fa-user-headset",
            UserId    = ticket.DealerId
        };
        _context.Notifications.Add(dealerNotification);
        await _context.SaveChangesAsync();

        // SignalR: bayiye gerçek zamanlı bildirim
        await _hub.Clients.Group($"Dealer_{ticket.DealerId}").SendAsync("ReceiveDealerNotification", new
        {
            TicketId  = ticketId,
            Message   = $"#{ticketId:D4} no'lu talebinize {assignedAdmin.FullName} atandı.",
            Timestamp = DateTime.Now.ToString("HH:mm")
        });

        _logger.LogInformation("Ticket #{Id} – Temsilci atandı: {Admin}", ticketId, assignedAdmin.FullName);
        TempData["Success"] = $"Ön temsilci olarak {assignedAdmin.FullName} atandı.";
        return RedirectToAction(nameof(Detail), new { id = ticketId });
    }

    // ─── CLOSE POST (Admin) ──────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Close(int id)
    {
        var ticket = await _context.SupportTickets
            .Include(t => t.Dealer)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();

        ticket.Status   = TicketStatus.Resolved;
        ticket.ClosedAt = DateTime.UtcNow;

        // Bayiye kapatma bildirimi
        var dealerNotification = new Notification
        {
            Title     = $"Destek Talebiniz Kapatıldı – #DST-{id:D4}",
            Message   = $"{ticket.Subject} başlıklı talebiniz çözüldü olarak işaretlendi.",
            Type      = "support",
            IconClass = "fa-circle-check",
            UserId    = ticket.DealerId
        };
        _context.Notifications.Add(dealerNotification);
        await _context.SaveChangesAsync();

        // SignalR: bayiye gerçek zamanlı bildirim
        await _hub.Clients.Group($"Dealer_{ticket.DealerId}").SendAsync("ReceiveDealerNotification", new
        {
            TicketId  = id,
            Message   = $"#{id:D4} no'lu talebiniz kapatıldı.",
            Timestamp = DateTime.Now.ToString("HH:mm")
        });

        TempData["Success"] = $"Talep #DST-{id:D4} kapatıldı.";
        return RedirectToAction(nameof(Index));
    }

    // ─── YARDIMCI: Dosya yükleme ─────────────────────────────────────────────────
    private async Task<string?> SaveAttachmentAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".xlsx", ".docx" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext)) return null;

        var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "tickets");
        Directory.CreateDirectory(uploadsPath);

        var uniqueName = $"{Guid.NewGuid()}{ext}";
        var filePath   = Path.Combine(uploadsPath, uniqueName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var attachment = new AttachmentItem
        {
            FileName = file.FileName,
            FilePath = $"/uploads/tickets/{uniqueName}",
            FileSize = file.Length
        };

        return JsonSerializer.Serialize(new[] { attachment });
    }
}

