using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NovaStock.Web.ViewModels;

// ─── Tedarikçi Form (Create / Edit) ─────────────────────────────────────────
public class SupplierFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tedarikçi adı zorunludur.")]
    [MaxLength(200)]
    [Display(Name = "Tedarikçi Adı")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    [Display(Name = "İlgili Kişi")]
    public string? ContactPerson { get; set; }

    [MaxLength(20)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [MaxLength(150)]
    [EmailAddress]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [MaxLength(500)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [MaxLength(50)]
    [Display(Name = "Vergi No")]
    public string? TaxNumber { get; set; }

    [Display(Name = "Açıklama")]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}

// ─── Tedarikçi Liste Satırı ───────────────────────────────────────────────────
public class SupplierListItem
{
    public int     Id            { get; set; }
    public string  Name          { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone         { get; set; }
    public string? Email         { get; set; }
    public decimal Balance       { get; set; }
    public bool    IsActive      { get; set; }
    public int     OrderCount    { get; set; }
    public decimal TotalPurchased { get; set; }
}

// ─── Mal Kabul (Satın Alma) ───────────────────────────────────────────────────
public class PurchaseViewModel
{
    [Required]
    [Display(Name = "Tedarikçi")]
    public int SupplierId { get; set; }

    [Display(Name = "Depo")]
    public int? WarehouseId { get; set; }

    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public List<PurchaseItemRow> Items { get; set; } = [];

    // SelectList helpers
    public List<SelectListItem> Suppliers  { get; set; } = [];
    public List<SelectListItem> Warehouses { get; set; } = [];
    public List<SelectListItem> Products   { get; set; } = [];
}

public class PurchaseItemRow
{
    [Required]
    public int ProductId { get; set; }

    [Required, Range(1, 100000)]
    public int Quantity { get; set; }

    [Required, Range(0.01, 9999999)]
    public decimal UnitCost { get; set; }
}
