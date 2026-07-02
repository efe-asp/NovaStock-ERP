using ClosedXML.Excel;
using NovaStock.Web.Models;

namespace NovaStock.Web.Services;

/// <summary>
/// Excel Export / Import servisi.
/// ClosedXML kullanarak .xlsx dosyası üretir ve okur.
/// </summary>
public class ExcelService
{
    private readonly ILogger<ExcelService> _logger;

    public ExcelService(ILogger<ExcelService> logger)
    {
        _logger = logger;
    }

    // ─── EXPORT ─────────────────────────────────────────────────────────────────
    /// <summary>Ürün listesini Excel dosyasına dönüştürür ve byte dizisi döner.</summary>
    public byte[] ExportProductsToExcel(IEnumerable<Product> products)
    {
        using var workbook  = new XLWorkbook();
        var       worksheet = workbook.Worksheets.Add("Ürünler");

        // Başlık satırı
        var headers = new[] { "ID", "SKU", "Ürün Adı", "Kategori", "Taban Fiyat (₺)", "Stok", "Kritik Stok", "Aktif?" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold              = true;
            cell.Style.Fill.BackgroundColor   = XLColor.FromHtml("#1a1f2e");
            cell.Style.Font.FontColor         = XLColor.FromHtml("#a78bfa");
            cell.Style.Alignment.Horizontal   = XLAlignmentHorizontalValues.Center;
        }

        // Veri satırları
        int row = 2;
        foreach (var p in products)
        {
            worksheet.Cell(row, 1).Value = p.Id;
            worksheet.Cell(row, 2).Value = p.SKU;
            worksheet.Cell(row, 3).Value = p.Name;
            worksheet.Cell(row, 4).Value = p.Category?.Name ?? "-";
            worksheet.Cell(row, 5).Value = p.BasePrice;
            worksheet.Cell(row, 6).Value = p.StockCount;
            worksheet.Cell(row, 7).Value = p.CriticalStockLevel;
            worksheet.Cell(row, 8).Value = p.IsActive ? "Evet" : "Hayır";

            // Kritik stok satırlarını kırmızı yap
            if (p.IsCriticalStock)
                worksheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#450a0a");

            row++;
        }

        // Sütun genişliğini otomatik ayarla
        worksheet.Columns().AdjustToContents();

        // Tablo filtresi ekle
        var range = worksheet.Range(1, 1, row - 1, headers.Length);
        range.SetAutoFilter();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ─── IMPORT ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Excel dosyasını okuyup ürün listesi döner.
    /// Hatalı satırlar atlanır; errors listesine eklenir.
    /// </summary>
    public (List<Product> Products, List<string> Errors) ImportProductsFromExcel(
        Stream fileStream, Dictionary<string, int> categoryMap)
    {
        var products = new List<Product>();
        var errors   = new List<string>();

        try
        {
            using var workbook  = new XLWorkbook(fileStream);
            var       worksheet = workbook.Worksheet(1);
            var       lastRow   = worksheet.LastRowUsed()?.RowNumber() ?? 1;

            for (int row = 2; row <= lastRow; row++)
            {
                var rowErrors = new List<string>();

                var sku      = worksheet.Cell(row, 1).GetValue<string>()?.Trim();
                var name     = worksheet.Cell(row, 2).GetValue<string>()?.Trim();
                var category = worksheet.Cell(row, 3).GetValue<string>()?.Trim();
                var priceStr = worksheet.Cell(row, 4).GetValue<string>()?.Trim();
                var stockStr = worksheet.Cell(row, 5).GetValue<string>()?.Trim();

                if (string.IsNullOrEmpty(sku))   rowErrors.Add($"Satır {row}: SKU boş olamaz.");
                if (string.IsNullOrEmpty(name))  rowErrors.Add($"Satır {row}: Ürün adı boş olamaz.");

                if (!decimal.TryParse(priceStr, out var price) || price < 0)
                    rowErrors.Add($"Satır {row}: Geçersiz fiyat değeri '{priceStr}'.");

                if (!int.TryParse(stockStr, out var stock) || stock < 0)
                    rowErrors.Add($"Satır {row}: Geçersiz stok değeri '{stockStr}'.");

                if (rowErrors.Count > 0)
                {
                    errors.AddRange(rowErrors);
                    continue;
                }

                var categoryId = category is not null && categoryMap.TryGetValue(category, out var cid)
                    ? cid
                    : categoryMap.Values.FirstOrDefault();

                products.Add(new Product
                {
                    SKU              = sku!,
                    Name             = name!,
                    BasePrice        = price,
                    StockCount       = stock,
                    CategoryId       = categoryId,
                    IsActive         = true,
                    CriticalStockLevel = 5
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel dosyası okunamadı.");
            errors.Add("Excel dosyası okunamadı: " + ex.Message);
        }

        return (products, errors);
    }
}
