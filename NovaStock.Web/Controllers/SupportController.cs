using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.Models;
using NovaStock.Web.ViewModels;
using System.Text.Json;

namespace NovaStock.Web.Controllers;

/// <summary>
/// B2B Destek Talepleri (Support Ticket / RMA Portal).
/// Bayiler destek talebi oluşturur, admin yanıtlar.
/// </summary>
[Authorize]
public class SupportController : Controller
{
    private readonly ApplicationDbContext         _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SupportController>   _logger;
    private readonly IWebHostEnvironment          _env;

    public SupportController(
        ApplicationDbContext          context,
        UserManager<ApplicationUser>  userManager,
        ILogger<SupportController>    logger,
        IWebHostEnvironment           env)
    {
        _context     = context;
        _userManager = userManager;
        _logger      = logger;
        _env         = env;
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

        var vm = new SupportTicketDetailViewModel
        {
            Id                 = ticket.Id,
            Subject            = ticket.Subject,
            Category           = ticket.Category,
            Priority           = ticket.Priority,
            Status             = ticket.Status,
            RelatedOrderNumber = ticket.RelatedOrderNumber,
            RelatedOrderId     = ticket.RelatedOrderId,
            AssignedToName     = ticket.AssignedTo?.FullName,
            DealerName         = ticket.Dealer?.FullName ?? "",
            DealerCompany      = ticket.Dealer?.CompanyName ?? "",
            CreatedAt          = ticket.CreatedAt,
            LastActivityAt     = ticket.LastActivityAt,
            IsAdmin            = isAdmin,
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
        await _context.SaveChangesAsync();

        _logger.LogInformation("Yeni destek talebi: #{Id} – {Subject} ({Category}) – Bayi: {Dealer}",
            ticket.Id, ticket.Subject, ticket.Category, currentUser.FullName);

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

        var ticket = await _context.SupportTickets.FindAsync(vm.TicketId);
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

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Detail), new { id = vm.TicketId });
    }

    // ─── CLOSE POST (Admin) ──────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Close(int id)
    {
        var ticket = await _context.SupportTickets.FindAsync(id);
        if (ticket is null) return NotFound();

        ticket.Status   = TicketStatus.Resolved;
        ticket.ClosedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

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
