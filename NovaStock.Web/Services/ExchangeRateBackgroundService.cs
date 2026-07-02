using NovaStock.Web.Data;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Models;

namespace NovaStock.Web.Services;

/// <summary>
/// Döviz kuru arka plan servisi.
/// Her sabah 09:00'da TCMB XML API'sinden güncel kurları çeker.
/// </summary>
public class ExchangeRateBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExchangeRateBackgroundService> _logger;
    private readonly HttpClient _httpClient;

    // TCMB Today XML URL
    private const string TcmbUrl = "https://www.tcmb.gov.tr/kurlar/today.xml";

    public ExchangeRateBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ExchangeRateBackgroundService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _logger          = logger;
        _httpClient      = httpClientFactory.CreateClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Döviz kuru servisi başlatıldı.");

        // İlk çalışmada hemen bir kez güncelle
        await UpdateRatesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Bir sonraki sabah 09:00'a kadar bekle
            var now  = DateTime.Now;
            var next = now.Date.AddDays(now.Hour >= 9 ? 1 : 0).AddHours(9);
            var delay = next - now;

            _logger.LogInformation("Sonraki döviz kuru güncellemesi: {Next}", next);
            await Task.Delay(delay, stoppingToken);
            await UpdateRatesAsync(stoppingToken);
        }
    }

    private async Task UpdateRatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("TCMB'den döviz kurları güncelleniyor...");

            // TCMB XML'ini çek
            var xml = await _httpClient.GetStringAsync(TcmbUrl, cancellationToken);

            var rates = ParseTcmbXml(xml);

            using var scope   = _serviceProvider.CreateScope();
            var       context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            foreach (var (code, rate) in rates)
            {
                var existing = await context.ExchangeRates
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.CurrencyCode == code, cancellationToken);

                if (existing is not null)
                {
                    existing.Rate      = rate;
                    existing.FetchedAt = DateTime.UtcNow;
                }
                else
                {
                    context.ExchangeRates.Add(new ExchangeRate
                    {
                        CurrencyCode = code,
                        Rate         = rate,
                        FetchedAt    = DateTime.UtcNow,
                        Source       = "TCMB"
                    });
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Döviz kurları güncellendi: {Count} para birimi", rates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Döviz kuru güncellenirken hata oluştu. Fallback değerler kullanılıyor.");
            await SaveFallbackRatesAsync(cancellationToken);
        }
    }

    /// <summary>TCMB XML parse – ForexSelling alanından kur okunur.</summary>
    private static Dictionary<string, decimal> ParseTcmbXml(string xml)
    {
        var rates = new Dictionary<string, decimal>();
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            var currencies = doc.Descendants("Currency");

            foreach (var currency in currencies)
            {
                var code     = currency.Attribute("CurrencyCode")?.Value;
                var selling  = currency.Element("ForexSelling")?.Value;

                if (code is not null && decimal.TryParse(
                        selling, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var rate))
                {
                    rates[code] = rate;
                }
            }
        }
        catch { /* XML parse hatası – fallback devrede */ }

        return rates;
    }

    /// <summary>TCMB erişilemediğinde tahmini kur kaydeder.</summary>
    private async Task SaveFallbackRatesAsync(CancellationToken cancellationToken)
    {
        using var scope   = _serviceProvider.CreateScope();
        var       context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var fallback = new Dictionary<string, decimal>
        {
            { "USD", 38.50m },
            { "EUR", 41.20m },
            { "GBP", 48.90m }
        };

        foreach (var (code, rate) in fallback)
        {
            var existing = await context.ExchangeRates
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.CurrencyCode == code, cancellationToken);

            if (existing is null)
            {
                context.ExchangeRates.Add(new ExchangeRate
                {
                    CurrencyCode = code,
                    Rate         = rate,
                    FetchedAt    = DateTime.UtcNow,
                    Source       = "Fallback"
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
