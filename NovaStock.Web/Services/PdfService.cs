using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NovaStock.Web.Models;
using NovaStock.Web.ViewModels;

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

    // ─── CARİ EKSTRE PDF ─────────────────────────────────────────────────
    /// <summary>
    /// Cari hesap ekstresi için resmi PDF belgesi üretir.
    /// Muhasebeci formatı: Ledger tablosu, toplam borç/alacak, kredi limiti.
    /// </summary>
    public byte[] GenerateStatementPdf(StatementViewModel vm)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                // HEADER
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("NovaStock ERP")
                            .FontSize(22).Bold().FontColor("#7c3aed");
                        col.Item().Text("Cari Hesap Ekstresi")
                            .FontSize(11).FontColor("#6b7280");
                    });
                    row.ConstantItem(180).Column(col =>
                    {
                        col.Item().AlignRight().Text("CARİ EKSTRE")
                            .FontSize(18).Bold().FontColor("#1f2937");
                        col.Item().AlignRight().Text($"Üretim: {DateTime.Now:dd.MM.yyyy HH:mm}")
                            .FontSize(9).FontColor("#6b7280");
                    });
                });

                page.Content().PaddingVertical(0.8f, Unit.Centimetre).Column(col =>
                {
                    // Bayi bilgi kutusu
                    col.Item().Border(1).BorderColor("#e5e7eb").Padding(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("BAYİ BİLGİLERİ").Bold().FontColor("#7c3aed").FontSize(10);
                            c.Item().Text(vm.DealerName);
                            c.Item().Text(vm.CompanyName).FontColor("#6b7280");
                            c.Item().Text($"Kademe: {vm.Tier} Bayi").FontColor("#6b7280");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("FİNANSAL ÖZET").Bold().FontColor("#7c3aed").FontSize(10);
                            c.Item().Text($"Net Borç: {vm.NetBalance:N2} ₺").FontColor("#dc2626").Bold();
                            c.Item().Text($"Kredi Limiti: {vm.CreditLimit:N2} ₺").FontColor("#6b7280");
                            c.Item().Text($"Vadesi Geçen: {vm.OverdueDebt:N2} ₺").FontColor("#dc2626");
                        });
                    });

                    col.Item().PaddingTop(16);

                    // Tablo başlıkları
                    col.Item().Background("#7c3aed").Padding(6).Row(row =>
                    {
                        row.ConstantItem(60).Text("Tarih").Bold().FontColor("#fff").FontSize(9);
                        row.RelativeItem(2).Text("İşlem Türü").Bold().FontColor("#fff").FontSize(9);
                        row.RelativeItem(2).Text("Evrak No").Bold().FontColor("#fff").FontSize(9);
                        row.ConstantItem(65).AlignRight().Text("Borç (₺)").Bold().FontColor("#fff").FontSize(9);
                        row.ConstantItem(65).AlignRight().Text("Alacak (₺)").Bold().FontColor("#fff").FontSize(9);
                        row.ConstantItem(70).AlignRight().Text("Bakiye (₺)").Bold().FontColor("#fff").FontSize(9);
                    });

                    bool alt = false;
                    foreach (var e in vm.Entries)
                    {
                        col.Item()
                           .Background(alt ? "#f9fafb" : "#ffffff")
                           .Padding(5).Row(row =>
                           {
                               row.ConstantItem(60).Text(e.TransactionDate.ToString("dd.MM.yy")).FontSize(9);
                               row.RelativeItem(2).Text(e.TransactionTypeLabel).FontSize(9);
                               row.RelativeItem(2).Text(e.DocumentNumber).FontSize(9).FontColor("#6b7280");
                               row.ConstantItem(65).AlignRight()
                                   .Text(e.Debit > 0 ? $"{e.Debit:N2}" : "–")
                                   .FontSize(9).FontColor(e.Debit > 0 ? "#dc2626" : "#9ca3af");
                               row.ConstantItem(65).AlignRight()
                                   .Text(e.Credit > 0 ? $"{e.Credit:N2}" : "–")
                                   .FontSize(9).FontColor(e.Credit > 0 ? "#16a34a" : "#9ca3af");
                               row.ConstantItem(70).AlignRight()
                                   .Text($"{e.RunningBalance:N2}").FontSize(9).Bold();
                           });
                        alt = !alt;
                    }

                    // Toplam satırı
                    col.Item().PaddingTop(8).AlignRight().Column(c =>
                    {
                        c.Item().Background("#7c3aed").Padding(8).Row(r =>
                        {
                            r.ConstantItem(150).Text("NET BAKIYE (TOPLAM BORÇ):").Bold().FontColor("#fff");
                            r.ConstantItem(100).AlignRight().Text($"{vm.NetBalance:N2} ₺").Bold().FontColor("#fff");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("NovaStock ERP – Cari Ekstre | Bu belge elektronik ortamda oluşturulmuştur. | Sayfa ");
                    text.CurrentPageNumber();
                    text.Span("/");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    // ─── PROFORMA FATURA PDF ──────────────────────────────────────────────
    /// <summary>
    /// Bayinin kendi müşterisine sunak üzere proforma fatura PDF'i üretir.
    /// Bayi kendi kâr marjını girer; şirket logosu yerine bayi adı yer alır.
    /// </summary>
    public byte[] GenerateProformaInvoice(Order order, decimal marginPercent, string dealerCompanyName)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                // HEADER – Bayi markalı
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(dealerCompanyName)
                            .FontSize(22).Bold().FontColor("#1f2937");
                        col.Item().Text("Proforma Fatura / Teklif Formu")
                            .FontSize(10).FontColor("#6b7280");
                    });
                    row.ConstantItem(160).Column(col =>
                    {
                        col.Item().AlignRight().Text("PROFORMA FATURA")
                            .FontSize(16).Bold().FontColor("#1f2937");
                        col.Item().AlignRight().Text($"Teklif No: TKF-{order.Id:D5}")
                            .FontSize(10).FontColor("#6b7280");
                        col.Item().AlignRight().Text($"Tarih: {DateTime.Now:dd.MM.yyyy}")
                            .FontSize(10).FontColor("#6b7280");
                    });
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                {
                    col.Item().PaddingTop(16);

                    // Ürün tablosu
                    col.Item().Background("#1f2937").Padding(8).Row(row =>
                    {
                        row.RelativeItem(3).Text("ÜRÜN").Bold().FontColor("#fff");
                        row.RelativeItem(1).AlignCenter().Text("ADET").Bold().FontColor("#fff");
                        row.RelativeItem(2).AlignRight().Text("BİRİM FİYAT").Bold().FontColor("#fff");
                        row.RelativeItem(2).AlignRight().Text("TOPLAM").Bold().FontColor("#fff");
                    });

                    bool alt = false;
                    foreach (var item in order.Items)
                    {
                        var sellingPrice = Math.Round(item.UnitPrice * (1 + marginPercent / 100), 2);
                        var lineTotal    = sellingPrice * item.Quantity;

                        col.Item()
                           .Background(alt ? "#f9fafb" : "#ffffff")
                           .Padding(8).Row(row =>
                           {
                               row.RelativeItem(3).Text(item.Product?.Name ?? "-");
                               row.RelativeItem(1).AlignCenter().Text(item.Quantity.ToString());
                               row.RelativeItem(2).AlignRight().Text($"{sellingPrice:N2} ₺");
                               row.RelativeItem(2).AlignRight().Text($"{lineTotal:N2} ₺").Bold();
                           });
                        alt = !alt;
                    }

                    var grandTotal = order.Items.Sum(i =>
                        Math.Round(i.UnitPrice * (1 + marginPercent / 100), 2) * i.Quantity);

                    col.Item().PaddingTop(10).AlignRight().Column(c =>
                    {
                        c.Item().Background("#1f2937").Padding(8).Row(r =>
                        {
                            r.ConstantItem(150).Text("GENEL TOPLAM:").Bold().FontColor("#fff");
                            r.ConstantItem(130).AlignRight().Text($"{grandTotal:N2} ₺").Bold().FontColor("#fff");
                        });
                    });

                    col.Item().PaddingTop(20);
                    col.Item().Text("NOT: Bu belge teklif niteliğinde olup, sipariş kesinleşmeden önce değişebilir.")
                        .FontSize(9).Italic().FontColor("#9ca3af");
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"{dealerCompanyName} | Proforma Fatura | Sayfa ");
                    text.CurrentPageNumber();
                    text.Span("/");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}

