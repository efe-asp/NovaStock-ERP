namespace NovaStock.Web.Services;

/// <summary>E-Posta gönderim servisi arayüzü.</summary>
public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    Task SendCriticalStockAlertAsync(string productName, int remainingStock, string adminEmail);
}
