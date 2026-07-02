using NovaStock.Web.Models;

namespace NovaStock.Web.Extensions;

/// <summary>
/// Bayi kademeleri için dinamik fiyatlandırma extension metotları.
/// Bronze: Taban fiyat | Silver: %5 indirim | Gold: %10 indirim
/// </summary>
public static class PricingExtensions
{
    private static readonly Dictionary<DealerTier, decimal> DiscountRates = new()
    {
        { DealerTier.Bronze, 0.00m },
        { DealerTier.Silver, 0.05m },
        { DealerTier.Gold,   0.10m }
    };

    /// <summary>
    /// Bayinin kademesine göre ürün fiyatını hesaplar.
    /// </summary>
    public static decimal GetPriceForTier(this Product product, DealerTier tier)
    {
        if (!DiscountRates.TryGetValue(tier, out var rate))
            return product.BasePrice;

        var discounted = product.BasePrice * (1 - rate);
        return Math.Round(discounted, 2);
    }

    /// <summary>
    /// Kademedeki indirim yüzdesini döner.
    /// </summary>
    public static decimal GetDiscountRate(this DealerTier tier)
        => DiscountRates.TryGetValue(tier, out var rate) ? rate * 100 : 0;

    /// <summary>
    /// Tier label badge CSS sınıfı.
    /// </summary>
    public static string GetBadgeClass(this DealerTier tier) => tier switch
    {
        DealerTier.Gold   => "badge-gold",
        DealerTier.Silver => "badge-silver",
        _                 => "badge-bronze"
    };

    /// <summary>
    /// Türkçe kur formatında fiyat gösterimi.
    /// </summary>
    public static string FormatPrice(this decimal price)
        => price.ToString("N2") + " ₺";
}
