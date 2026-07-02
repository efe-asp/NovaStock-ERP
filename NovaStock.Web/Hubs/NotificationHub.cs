using Microsoft.AspNetCore.SignalR;

namespace NovaStock.Web.Hubs;

/// <summary>
/// SignalR Hub – Anlık sipariş bildirimleri.
/// Admin panelinde sayfa yenilenmeden bildirim düşer.
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
    /// Admin bağlandığında "Admins" grubuna ekle.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }

        await base.OnConnectedAsync();
    }
}
