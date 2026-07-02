using NovaStock.Web.Models;

namespace NovaStock.Web.Services;

/// <summary>
/// Kampanya ve indirim kurallarını işleyen motor.
/// Strateji Pattern ile her kural ayrı sınıfta uygulanır.
/// </summary>
public class PromotionEngine
{
    private readonly ILogger<PromotionEngine> _logger;

    public PromotionEngine(ILogger<PromotionEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Verilen sipariş kalemleri ve aktif kampanyalar üzerinden
    /// toplam indirimi hesaplar.
    /// </summary>
    public PromotionResult Apply(
        IEnumerable<OrderItem>  items,
        IEnumerable<Promotion>  promotions,
        ApplicationUser?        dealer = null)
    {
        var itemList  = items.ToList();
        var promoList = promotions.Where(p => p.IsActive).ToList();

        decimal subTotal      = itemList.Sum(i => i.UnitPrice * i.Quantity);
        decimal totalDiscount = 0m;
        var     appliedNames  = new List<string>();

        foreach (var promo in promoList)
        {
            if (promo.ValidUntil.HasValue && promo.ValidUntil < DateTime.UtcNow)
                continue;

            decimal discount = 0m;

            switch (promo.Type)
            {
                case PromotionType.PercentageDiscount:
                    if (promo.MinimumCartTotal.HasValue && subTotal < promo.MinimumCartTotal.Value)
                        break;
                    discount = subTotal * (promo.DiscountValue / 100m);
                    break;

                case PromotionType.FixedDiscount:
                    if (promo.MinimumCartTotal.HasValue && subTotal < promo.MinimumCartTotal.Value)
                        break;
                    discount = promo.DiscountValue;
                    break;

                case PromotionType.FreeShipping:
                    // Kargo ücreti 0 olarak işaretlenir (bu projede sabit tutuluyor)
                    discount = 0m;
                    break;

                case PromotionType.BuyXGetY:
                    // Her 3 adet için 1 adet ücretsiz (en ucuz ürün)
                    var categoryItems = promo.CategoryId.HasValue
                        ? itemList.Where(i => i.Product?.CategoryId == promo.CategoryId)
                        : itemList;

                    foreach (var item in categoryItems)
                    {
                        if (promo.MinimumQuantity.HasValue && item.Quantity >= promo.MinimumQuantity)
                        {
                            var freeCount = item.Quantity / 3; // Her 3'te 1 bedava
                            discount += freeCount * item.UnitPrice;
                        }
                    }
                    break;
            }

            if (discount > 0)
            {
                totalDiscount += discount;
                appliedNames.Add(promo.Name);
                _logger.LogInformation("Kampanya uygulandı: {Name}, İndirim: {Discount}", promo.Name, discount);
            }
        }

        // İndirim sepet toplamını geçemez
        totalDiscount = Math.Min(totalDiscount, subTotal);

        return new PromotionResult
        {
            SubTotal       = subTotal,
            DiscountAmount = Math.Round(totalDiscount, 2),
            Total          = Math.Round(subTotal - totalDiscount, 2),
            AppliedPromotions = appliedNames
        };
    }
}

public class PromotionResult
{
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public List<string> AppliedPromotions { get; set; } = [];
}
