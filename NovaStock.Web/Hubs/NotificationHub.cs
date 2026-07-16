using Microsoft.AspNetCore.SignalR;

namespace NovaStock.Web.Hubs;

/// <summary>
/// SignalR Hub – Anlık sipariş ve destek talebi bildirimleri.
/// Admin panelinde ve bayi portalında sayfa yenilenmeden bildirim düşer.
/// </summary>
public class NotificationHub : Hub
{
    /// <summary>
    /// Admin grubuna yeni sipariş bildirimi gönderir.
    /// </summary>
    public async Task SendOrderNotification(string dealerName, decimal total, int orderId)
    {
        await Clients.Group("Admins").SendAsync("ReceiveOrderNotification", new
        {
            DealerName  = dealerName,
            Total       = total.ToString("N2"),
            OrderId     = orderId,
            Timestamp   = DateTime.Now.ToString("HH:mm")
        });
    }

    /// <summary>
    /// Kritik stok uyarısı yayını.
    /// </summary>
    public async Task SendStockAlert(string productName, int remainingStock)
    {
        await Clients.Group("Admins").SendAsync("ReceiveStockAlert", new
        {
            ProductName    = productName,
            RemainingStock = remainingStock,
            Timestamp      = DateTime.Now.ToString("HH:mm")
        });
    }

    /// <summary>
    /// Admin grubuna yeni destek talebi bildirimi gönderir.
    /// Bayi tarafından yeni ticket veya mesaj geldiğinde çağrılır.
    /// </summary>
    public async Task SendSupportTicketNotification(int ticketId, string subject, string dealerName)
    {
        await Clients.Group("Admins").SendAsync("ReceiveSupportTicketNotification", new
        {
            TicketId   = ticketId,
            Subject    = subject,
            DealerName = dealerName,
            Timestamp  = DateTime.Now.ToString("HH:mm")
        });
    }

    /// <summary>
    /// Belirli bir bayiye (kişisel grubuna) bildirim gönderir.
    /// Admin yanıt verdiğinde veya ticket durumu değiştiğinde çağrılır.
    /// </summary>
    public async Task SendDealerNotification(string dealerId, int ticketId, string message)
    {
        await Clients.Group($"Dealer_{dealerId}").SendAsync("ReceiveDealerNotification", new
        {
            TicketId  = ticketId,
            Message   = message,
            Timestamp = DateTime.Now.ToString("HH:mm")
        });
    }

    /// <summary>
    /// Kullanıcı bağlandığında rol/kimliğe göre gruba ekle.
    /// - Admin → "Admins" grubu
    /// - Bayi → "Dealer_{userId}" kişisel grubu
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }
        else if (Context.User?.Identity?.IsAuthenticated == true)
        {
            // Bayi veya alt kullanıcı: kendi kişisel grubuna ekle
            var userId = Context.User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Dealer_{userId}");
            }
        }

        await base.OnConnectedAsync();
    }
}
