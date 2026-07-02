using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace NovaStock.Web.Services;

/// <summary>
/// MailKit tabanlı SMTP e-posta servisi.
/// Kritik stok alarmı ve sipariş bildirimleri için kullanılır.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var smtpHost     = _config["Email:SmtpHost"]     ?? "smtp.gmail.com";
            var smtpPort     = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var smtpUser     = _config["Email:Username"]     ?? "";
            var smtpPassword = _config["Email:Password"]     ?? "";
            var fromName     = _config["Email:FromName"]     ?? "NovaStock ERP";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, smtpUser));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("E-posta gönderildi: {To} | Konu: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-posta gönderilemedi: {To}", toEmail);
        }
    }

    public async Task SendCriticalStockAlertAsync(string productName, int remainingStock, string adminEmail)
    {
        var subject = $"⚠️ Kritik Stok Uyarısı: {productName}";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background: #0d1117; color: #e6edf3; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.1); border-radius: 12px; overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #dc2626, #7f1d1d); padding: 30px; text-align: center; }}
        .header h1 {{ margin: 0; font-size: 24px; color: #fff; }}
        .body {{ padding: 30px; }}
        .alert-box {{ background: rgba(220,38,38,0.1); border: 1px solid rgba(220,38,38,0.3); border-radius: 8px; padding: 20px; margin: 20px 0; }}
        .product-name {{ font-size: 20px; font-weight: bold; color: #fca5a5; }}
        .stock-count {{ font-size: 48px; font-weight: 900; color: #dc2626; text-align: center; margin: 10px 0; }}
        .footer {{ background: rgba(0,0,0,0.3); padding: 15px 30px; font-size: 12px; color: #6e7681; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>⚠️ KRİTİK STOK UYARISI</h1>
        </div>
        <div class='body'>
            <div class='alert-box'>
                <p>Aşağıdaki ürün kritik stok seviyesine düşmüştür:</p>
                <div class='product-name'>{productName}</div>
                <div class='stock-count'>{remainingStock} adet</div>
                <p>Lütfen en kısa sürede tedarik işlemini başlatın.</p>
            </div>
            <p>Bu bildirim NovaStock ERP sistemi tarafından otomatik olarak gönderilmiştir.</p>
        </div>
        <div class='footer'>
            NovaStock ERP &copy; {DateTime.Now.Year} | {DateTime.Now:dd.MM.yyyy HH:mm}
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(adminEmail, subject, body);
    }
}
