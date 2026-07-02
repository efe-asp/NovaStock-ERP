using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NovaStock.Web.Models;

namespace NovaStock.Web.Services;

/// <summary>
/// QuestPDF tabanlı sipariş faturası üretici.
/// Kurumsal PDF şablonu – şirket logosu ve sipariş detayları.
/// </summary>
public class PdfService
{
    static PdfService()
    {
        // QuestPDF community lisansı
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Sipariş için PDF fatura byte dizisi üretir.</summary>
    public byte[] GenerateInvoice(Order order)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                // ─── HEADER ─────────────────────────────────────────────────
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("NovaStock ERP")
                            .FontSize(24).Bold().FontColor("#7c3aed");
                        col.Item().Text("Kurumsal Stok & Sipariş Yönetimi")
                            .FontSize(10).FontColor("#6b7280");
                    });

                    row.ConstantItem(150).Column(col =>
                    {
                        col.Item().AlignRight().Text($"FATURA")
                            .FontSize(20).Bold().FontColor("#1f2937");
                        col.Item().AlignRight().Text($"#{order.OrderNumber}")
                            .FontSize(12).FontColor("#6b7280");
                        col.Item().AlignRight().Text(order.CreatedAt.ToString("dd.MM.yyyy"))
                            .FontSize(10).FontColor("#6b7280");
                    });
                });

                // ─── CONTENT ────────────────────────────────────────────────
                page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                {
                    // Sipariş & Bayi Bilgileri
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor("#e5e7eb").Padding(10).Column(c =>
                        {
                            c.Item().Text("FATURA BİLGİLERİ").Bold().FontColor("#7c3aed");
                            c.Item().Text($"Sipariş No: {order.OrderNumber}");
                            c.Item().Text($"Tarih: {order.CreatedAt:dd.MM.yyyy HH:mm}");
                            c.Item().Text($"Durum: {order.Status}");
                        });

                        row.ConstantItem(20);

                        row.RelativeItem().Border(1).BorderColor("#e5e7eb").Padding(10).Column(c =>
                        {
                            c.Item().Text("BAYİ BİLGİLERİ").Bold().FontColor("#7c3aed");
                            c.Item().Text(order.Dealer?.FullName ?? "-");
                            c.Item().Text(order.Dealer?.CompanyName ?? "-");
                            c.Item().Text(order.Dealer?.Email ?? "-");
                            if (!string.IsNullOrEmpty(order.BillingAddress))
                                c.Item().Text(order.BillingAddress);
                        });
                    });

                    col.Item().PaddingTop(20);

                    // Ürün Tablosu Başlıkları
                    col.Item().Background("#7c3aed").Padding(8).Row(row =>
                    {
                        row.RelativeItem(3).Text("ÜRÜN").Bold().FontColor("#ffffff");
                        row.RelativeItem(1).AlignCenter().Text("ADET").Bold().FontColor("#ffffff");
                        row.RelativeItem(2).AlignRight().Text("BİRİM FİYAT").Bold().FontColor("#ffffff");
                        row.RelativeItem(2).AlignRight().Text("TOPLAM").Bold().FontColor("#ffffff");
                    });

                    // Ürün Satırları
                    bool alt = false;
                    foreach (var item in order.Items)
                    {
                        col.Item()
                           .Background(alt ? "#f9fafb" : "#ffffff")
                           .Padding(8)
                           .Row(row =>
                           {
                               row.RelativeItem(3).Text(item.Product?.Name ?? "-");
                               row.RelativeItem(1).AlignCenter().Text(item.Quantity.ToString());
                               row.RelativeItem(2).AlignRight().Text($"{item.UnitPrice:N2} ₺");
                               row.RelativeItem(2).AlignRight().Text($"{item.UnitPrice * item.Quantity:N2} ₺");
                           });
                        alt = !alt;
                    }

                    // Toplam
                    col.Item().PaddingTop(10).AlignRight().Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.ConstantItem(150).Text("Ara Toplam:").Bold();
                            r.ConstantItem(120).AlignRight().Text($"{order.SubTotal:N2} ₺");
                        });

                        if (order.DiscountAmount > 0)
                        {
                            c.Item().Row(r =>
                            {
                                r.ConstantItem(150).Text("İndirim:").FontColor("#dc2626");
                                r.ConstantItem(120).AlignRight().Text($"-{order.DiscountAmount:N2} ₺").FontColor("#dc2626");
                            });
                        }

                        c.Item().Background("#7c3aed").Padding(8).Row(r =>
                        {
                            r.ConstantItem(150).Text("GENEL TOPLAM:").Bold().FontColor("#ffffff");
                            r.ConstantItem(120).AlignRight().Text($"{order.Total:N2} ₺").Bold().FontColor("#ffffff");
                        });
                    });
                });

                // ─── FOOTER ─────────────────────────────────────────────────
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("NovaStock ERP | ");
                    text.Span("Bu belge elektronik ortamda oluşturulmuştur. | ");
                    text.CurrentPageNumber();
                    text.Span("/");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
