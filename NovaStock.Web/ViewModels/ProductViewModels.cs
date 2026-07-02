using System.ComponentModel.DataAnnotations;

namespace NovaStock.Web.ViewModels;

/// <summary>Dinamik LINQ filtreleme formu.</summary>
public class ProductFilterViewModel
{
    public string? SearchTerm       { get; set; }
    public int?    CategoryId       { get; set; }
    public decimal? MinPrice        { get; set; }
    public decimal? MaxPrice        { get; set; }
    public bool    InStockOnly      { get; set; } = false;
    public bool    CriticalOnly     { get; set; } = false;
    public string  SortBy           { get; set; } = "Name";
    public string  SortOrder        { get; set; } = "asc";
    public int     Page             { get; set; } = 1;
    public int     PageSize         { get; set; } = 20;
}

/// <summary>Ürün oluşturma / düzenleme formu.</summary>
public class ProductFormViewModel
{
    public int     Id                { get; set; }

    [Required(ErrorMessage = "Ürün adı zorunludur.")]
    [MaxLength(200)]
    public string  Name              { get; set; } = string.Empty;

    [Required(ErrorMessage = "SKU zorunludur.")]
    [MaxLength(50)]
    public string  SKU               { get; set; } = string.Empty;

    public string? Description       { get; set; }

    [Required, Range(0.01, double.MaxValue, ErrorMessage = "Fiyat 0'dan büyük olmalıdır.")]
    public decimal BasePrice         { get; set; }

    [Required, Range(0, int.MaxValue)]
    public int     StockCount        { get; set; }

    [Range(1, 100)]
    public int     CriticalStockLevel { get; set; } = 5;

    [Required(ErrorMessage = "Kategori seçiniz.")]
    public int     CategoryId        { get; set; }

    public bool    IsActive          { get; set; } = true;
    public string? ImageUrl          { get; set; }

    // Dropdown için kategoriler
    public List<CategorySelectItem> Categories { get; set; } = [];
}

public class CategorySelectItem
{
    public int    Id   { get; set; }
    public string Name { get; set; } = string.Empty;
}
