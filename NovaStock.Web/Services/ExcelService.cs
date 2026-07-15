using ClosedXML.Excel;
using NovaStock.Web.Models;
using NovaStock.Web.ViewModels;

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

    // ─── CARİ EKSTRE EXCEL EXPORT ────────────────────────────────────────────────
    /// <summary>
    /// Cari hesap ekstresini Excel dosyasına dönüştürür.
    /// Muhasebeci formatında, tam ledger tablosu çıktısı verir.
    /// </summary>
    public byte[] ExportStatementExcel(string companyName, string dealerName,
        IEnumerable<LedgerEntryViewModel> entries, decimal totalDebt, decimal creditLimit)
    {
        using var workbook  = new XLWorkbook();
        var       ws        = workbook.Worksheets.Add("Cari Ekstre");

        // Başlık satırı
        ws.Cell(1, 1).Value = $"CARİ HESAP EKSTRE Sİ – {companyName.ToUpper()}";
        ws.Range("A1:G1").Merge();
        var titleCell = ws.Cell(1, 1);
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 14;
        titleCell.Style.Font.FontColor = XLColor.FromHtml("#7c3aed");
        titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(2, 1).Value = $"Bayi: {dealerName}  |  Oluşturma: {DateTime.Now:dd.MM.yyyy HH:mm}  |  Toplam Borç: {totalDebt:N2} ₺  |  Kredi Limiti: {creditLimit:N2} ₺";
        ws.Range("A2:G2").Merge();
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#6b7280");
        ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Sütun başlıkları
        var headers = new[] { "Tarih", "İşlem Türü", "Evrak No", "Borç (₺)", "Alacak (₺)", "Kalan Bakiye (₺)", "Açıklama" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#7c3aed");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        // Veri satırları
        int row = 5;
        bool alt = false;
        foreach (var e in entries)
        {
            ws.Cell(row, 1).Value = e.TransactionDate.ToString("dd.MM.yyyy HH:mm");
            ws.Cell(row, 2).Value = e.TransactionTypeLabel;
            ws.Cell(row, 3).Value = e.DocumentNumber;
            ws.Cell(row, 4).Value = e.Debit > 0 ? e.Debit : (decimal?)null;
            ws.Cell(row, 5).Value = e.Credit > 0 ? e.Credit : (decimal?)null;
            ws.Cell(row, 6).Value = e.RunningBalance;
            ws.Cell(row, 7).Value = e.Description;

            if (alt) ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#f9fafb");

            // Borç satırlarını kırmızı, alacak satırlarını yeşil yap
            if (e.Debit > 0) ws.Cell(row, 4).Style.Font.FontColor = XLColor.FromHtml("#dc2626");
            if (e.Credit > 0) ws.Cell(row, 5).Style.Font.FontColor = XLColor.FromHtml("#16a34a");

            // Para formatı
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";

            alt = !alt;
            row++;
        }

        // Toplam satırı
        ws.Cell(row, 1).Value = "GENEL TOPLAM";
        ws.Range(row, 1, row, 3).Merge();
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 4).Value = totalDebt;
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Cell(row, 4).Style.Font.FontColor = XLColor.FromHtml("#dc2626");
        ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3e8ff");

        ws.Columns().AdjustToContents();
        ws.Range(4, 1, row, headers.Length).SetAutoFilter();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── TOPLU SİPARİŞ EXCEL PARSE ──────────────────────────────────────────────
    /// <summary>
    /// İki kolonlu (SKU, Adet) Excel dosyasını okur.
    /// SKU-adet çiftleri döner, hatalı satırları errors listesine ekler.
    /// </summary>
    public (List<BulkOrderRow> Rows, List<string> Errors) ParseBulkOrderExcel(Stream fileStream)
    {
        var rows   = new List<BulkOrderRow>();
        var errors = new List<string>();

        try
        {
            using var workbook  = new XLWorkbook(fileStream);
            var       ws        = workbook.Worksheet(1);
            var       lastRow   = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = 2; r <= lastRow; r++)
            {
                var sku     = ws.Cell(r, 1).GetValue<string>()?.Trim();
                var qtyStr  = ws.Cell(r, 2).GetValue<string>()?.Trim();

                if (string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(qtyStr)) continue;

                if (string.IsNullOrEmpty(sku))
                {
                    errors.Add($"Satır {r}: SKU boş olamaz.");
                    continue;
                }

                if (!int.TryParse(qtyStr, out var qty) || qty <= 0)
                {
                    errors.Add($"Satır {r}: '{sku}' için geçersiz adet değeri '{qtyStr}'.");
                    continue;
                }

                rows.Add(new BulkOrderRow { SKU = sku, Quantity = qty, RowNumber = r });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Toplu sipariş Excel dosyası okunamadı.");
            errors.Add("Excel dosyası okunamadı: " + ex.Message);
        }

        return (rows, errors);
    }

    /// <summary>Toplu sipariş için boş şablon Excel dosyası üretir.</summary>
    public byte[] GenerateBulkOrderTemplate()
    {
        using var workbook  = new XLWorkbook();
        var       ws        = workbook.Worksheets.Add("Toplu Sipariş");

        ws.Cell(1, 1).Value = "SKU (Zorunlu)";
        ws.Cell(1, 2).Value = "Adet (Zorunlu)";

        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#7c3aed");
        ws.Row(1).Style.Font.FontColor = XLColor.White;

        // Örnek satırlar
        ws.Cell(2, 1).Value = "APP-IPH15PM-256";
        ws.Cell(2, 2).Value = 5;
        ws.Cell(3, 1).Value = "HUA-WGT4-BLK";
        ws.Cell(3, 2).Value = 10;

        ws.Row(2).Style.Font.FontColor = XLColor.FromHtml("#9ca3af");
        ws.Row(3).Style.Font.FontColor = XLColor.FromHtml("#9ca3af");

        ws.Cell(5, 1).Value = "* 2. satırdan itibaren kendi ürün kodlarınızı ve adetleri girin.";
        ws.Cell(5, 1).Style.Font.Italic = true;
        ws.Cell(5, 1).Style.Font.FontColor = XLColor.FromHtml("#6b7280");

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── DİNAMİK FİYAT MATRİSİ EXPORT ─────────────────────────────────────────────
    /// <summary>
    /// Tier'a göre kişisel indirimli fiyat matrisi Excel çıktısı.
    /// Gold: %15 indirim | Silver: %8 indirim | Bronze: liste fiyatı.
    /// </summary>
    public byte[] ExportPriceMatrix(IEnumerable<Product> products, DealerTier tier, string dealerName)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Fiyat Listesi");

        var (discountRate, tierLabel, tierColor) = tier switch
        {
            DealerTier.Gold   => (0.15m, "GOLD BAYİ – %15 Özel İndirim", "#f59e0b"),
            DealerTier.Silver => (0.08m, "SILVER BAYİ – %8 Özel İndirim", "#64748b"),
            _                 => (0.00m, "BRONZE BAYİ – Liste Fiyatı", "#b45309")
        };

        // Başlık
        ws.Cell(1, 1).Value = $"NOVASTOCK ERP – KİŞİSEL FİYAT LİSTESİ";
        ws.Range("A1:G1").Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#7c3aed");

        ws.Cell(2, 1).Value = $"{tierLabel}  |  Bayi: {dealerName}  |  {DateTime.Now:dd.MM.yyyy}";
        ws.Range("A2:G2").Merge();
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml(tierColor);
        ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var headers = new[] { "SKU", "Ürün Adı", "Kategori", "Stok Durumu", "Liste Fiyatı (₺)", $"Bayi Fiyatınız (₺)", "Tasarruf (₺)" };
        for (int i = 0; i < headers.Length; i++)
        {
            var c = ws.Cell(4, i + 1);
            c.Value = headers[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml(tierColor);
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int row = 5;
        bool alt = false;
        foreach (var p in products.Where(x => x.IsActive))
        {
            var dealerPrice = Math.Round(p.BasePrice * (1 - discountRate), 2);
            var saving      = p.BasePrice - dealerPrice;

            ws.Cell(row, 1).Value = p.SKU;
            ws.Cell(row, 2).Value = p.Name;
            ws.Cell(row, 3).Value = p.Category?.Name ?? "-";
            ws.Cell(row, 4).Value = p.IsCriticalStock ? "⚠ Düşük Stok" : $"{p.StockCount} Adet";
            ws.Cell(row, 5).Value = p.BasePrice;
            ws.Cell(row, 6).Value = dealerPrice;
            ws.Cell(row, 7).Value = saving;

            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 6).Style.Font.FontColor = XLColor.FromHtml("#16a34a");
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 7).Style.Font.FontColor = XLColor.FromHtml("#16a34a");

            if (p.IsCriticalStock) ws.Cell(row, 4).Style.Font.FontColor = XLColor.FromHtml("#dc2626");
            if (alt) ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#faf5ff");

            alt = !alt;
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.Range(4, 1, row - 1, headers.Length).SetAutoFilter();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}

