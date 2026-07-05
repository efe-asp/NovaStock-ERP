using System.ComponentModel.DataAnnotations;
using NovaStock.Web.Models;

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

    [MaxLength(100)]
    public string? Barcode           { get; set; }

    public string? Description       { get; set; }

    [Required, Range(0.01, double.MaxValue, ErrorMessage = "Fiyat 0'dan büyük olmalıdır.")]
    public decimal BasePrice         { get; set; }

    public decimal? CostPrice        { get; set; }

    public int     VatRate           { get; set; } = 20;

    [Required, Range(0, int.MaxValue)]
    public int     StockCount        { get; set; }

    [Range(1, 100)]
    public int     CriticalStockLevel { get; set; } = 5;

    [Required(ErrorMessage = "Kategori seçiniz.")]
    public int     CategoryId        { get; set; }

    public int?    SupplierId        { get; set; }

    public bool    IsActive          { get; set; } = true;
    public string? ImageUrl          { get; set; }

    // JSON olarak saklanacak detaylar
    public string? SpecsJson         { get; set; }

    // Dropdown için kategoriler ve tedarikçiler
    public List<CategorySelectItem> Categories { get; set; } = [];
    public List<SupplierSelectItem> Suppliers { get; set; } = [];
}

/// <summary>Ürün detay sayfası için ViewModel.</summary>
public class ProductDetailViewModel
{
    public Product                          Product        { get; set; } = null!;
    public ProductSpecsData                 Specs          { get; set; } = new();
    // Son 6 aylık satış verileri (grafik için)
    public List<MonthlySalesStat>           MonthlySales   { get; set; } = [];
    // Son siparişler (bu üründe)
    public List<ProductRecentOrderItem>         RecentOrders   { get; set; } = [];
}

public class MonthlySalesStat
{
    public string Month    { get; set; } = string.Empty;
    public int    Quantity { get; set; }
    public decimal Revenue { get; set; }
}

public class ProductRecentOrderItem
{
    public int     OrderId      { get; set; }
    public string  OrderNumber  { get; set; } = string.Empty;
    public string  DealerName   { get; set; } = string.Empty;
    public int     Quantity     { get; set; }
    public decimal UnitPrice    { get; set; }
    public DateTime OrderDate   { get; set; }
    public string  Status       { get; set; } = string.Empty;
}

/// <summary>SpecsJson kolonunun içeriğini temsil eden POCO sınıfları.</summary>
public class ProductSpecsData
{
    public List<string>                 ImageUrls    { get; set; } = [];
    public Dictionary<string, string>   Technical    { get; set; } = [];
    public ProductLogistics             Logistics    { get; set; } = new();
    public ProductCompliance            Compliance   { get; set; } = new();
}

public class ProductLogistics
{
    public decimal? WeightGrams  { get; set; }
    public decimal? WidthCm      { get; set; }
    public decimal? HeightCm     { get; set; }
    public decimal? DepthCm      { get; set; }
    public decimal? Desi         { get; set; }
}

public class ProductCompliance
{
    public string?       GtipCode        { get; set; }
    public int?          WarrantyMonths  { get; set; }
    public string?       Origin          { get; set; }
    public List<string>  Certifications  { get; set; } = [];
}

public class CategorySelectItem
{
    public int    Id   { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SupplierSelectItem
{
    public int    Id   { get; set; }
    public string Name { get; set; } = string.Empty;
}

