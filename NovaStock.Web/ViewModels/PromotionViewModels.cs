using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using NovaStock.Web.Models;

namespace NovaStock.Web.ViewModels;

public class PromotionFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kampanya adı zorunludur.")]
    [MaxLength(200)]
    [Display(Name = "Kampanya Adı")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Kampanya Tipi")]
    public PromotionType Type { get; set; }

    [Required, Range(0.01, 999999)]
    [Display(Name = "İndirim Değeri")]
    public decimal DiscountValue { get; set; }

    [Range(0, 9999999)]
    [Display(Name = "Min. Sepet Tutarı (₺)")]
    public decimal? MinimumCartTotal { get; set; }

    [Range(1, 100000)]
    [Display(Name = "Min. Ürün Adedi")]
    public int? MinimumQuantity { get; set; }

    [Display(Name = "Kategori (opsiyonel)")]
    public int? CategoryId { get; set; }

    public bool IsActive { get; set; } = true;

    [Display(Name = "Bitiş Tarihi")]
    public DateTime? ValidUntil { get; set; }

    // SelectList helper
    public List<SelectListItem> Categories { get; set; } = [];
}
