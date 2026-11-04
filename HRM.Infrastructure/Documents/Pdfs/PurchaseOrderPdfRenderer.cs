using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
namespace HRM.Infrastructure.Documents.Pdfs
{
    internal sealed class PurchaseOrderPdfRenderer(IOptions<PdfOptions> options, IHostEnvironment environment) : IPurchaseOrderPdfRenderer
    {
        public byte[] RenderTemplate()
        {
            return Render(new PurchaseOrderPdfDocument(), true);
        }

        public byte[] Render(PurchaseOrderPdfDocument d, bool templateOnly = false)
        {
            d ??= new PurchaseOrderPdfDocument();

            var doc = Document.Create(c =>
            {
                c.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(t => t.FontFamily("Open Sans").FontSize(8));

                    page.Header().Component(new PurchaseOrderHeader(environment.ContentRootPath, options.Value.Quotation.LogoPath));
                    page.Content().Element(x => BuildContent(x, d, templateOnly));
                    page.Footer().Component(new PurchaseOrderFooter(environment.ContentRootPath));
                });
            });

            return doc.GeneratePdf();
        }

        internal static (decimal Subtotal, decimal VatAmount, decimal GrandTotal) CalculateTotals(PurchaseOrderPdfDocument document)
        {
            var subtotal = document.Details.Sum(line => line.TotalUnitPriceAgreed ?? 0m);
            var vatAmount = PurchaseOrderTotals.VatAmount(subtotal, document.Vat);
            var grandTotal = document.TotalPrice ?? PurchaseOrderTotals.Calculate(subtotal, document.Vat);
            return (subtotal, vatAmount, grandTotal);
        }

        private void BuildContent(IContainer c, PurchaseOrderPdfDocument d, bool templateOnly)
        {
            d ??= new PurchaseOrderPdfDocument();

            var details = templateOnly
                ? new List<PurchaseOrderPdfLine>()
                : (d.Details?.ToList() ?? new List<PurchaseOrderPdfLine>());

            c.Column(col =>
            {
                col.Item().AlignCenter().PaddingTop(5)
                    .Text("ĐƠN ĐẶT HÀNG / PURCHASING ORDER")
                    .FontSize(10).Bold();

                col.Item().Row(row =>
                {
                    col.Item().PaddingBottom(5).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(90);
                            cols.RelativeColumn(2);
                            cols.ConstantColumn(80);
                            cols.RelativeColumn();
                        });

                        static IContainer CellStyle(IContainer container) =>
                            container.BorderBottom(1)
                                     .BorderColor(Colors.Grey.Lighten2)
                                     .PaddingVertical(2);

                        void AddRow(string leftLabel, string? leftValue, string rightLabel, string? rightValue)
                        {
                            table.Cell().Element(CellStyle).Text(leftLabel).SemiBold();
                            table.Cell().Element(CellStyle).Text(string.IsNullOrWhiteSpace(leftValue) ? "" : leftValue);

                            table.Cell().Element(CellStyle).Text(rightLabel).SemiBold();
                            table.Cell().Element(CellStyle).Text(string.IsNullOrWhiteSpace(rightValue) ? "" : rightValue);
                        }

                        var dateStr = templateOnly
                            ? "....../....../......"
                            : (d.CreatedDate ?? DateTime.Now).ToString("dd/MM/yyyy");

                        AddRow("Kính gửi/To:", templateOnly ? "" : d.SupplierNameSnapshot, "ĐĐH SỐ/PO No:", templateOnly ? "" : d.PurchaseOrderExternalIdSnapshot);
                        AddRow("Ông/Bà (Mr/Ms):", templateOnly ? "" : d.SupplierContactSnapshot, "Ngày/Date:", dateStr);
                        AddRow("Đồng gửi/Cc:", "", "Từ/From:", templateOnly ? "" : d.EmployeeFullNameSnapshot);
                        AddRow("Địa chỉ/Address:", templateOnly ? "" : d.SupplierAddressSnapshot, "Điện thoại/Tel:", templateOnly ? "" : d.PhoneNumberSnapshot);
                        AddRow("Điện thoại/Tel:", templateOnly ? "" : d.SupplierPhoneNumberSnapshot, "Website:", templateOnly ? "" : "https://vietaus.com");
                    });
                });

                col.Item().PaddingTop(2).Text(txt =>
                {
                    txt.Span("Công ty chúng tôi có nhu cầu đặt các nguyên liệu sau:/Our company has plan to order the raw material below:\r\n");
                });

                col.Item().PaddingTop(2).Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(3.5f);
                        cd.RelativeColumn(1.0f);
                        cd.RelativeColumn(1.0f);
                        cd.RelativeColumn(1.0f);
                        cd.RelativeColumn(1.0f);
                    });

                    static IContainer Th(IContainer x) => x
                        .Background(Colors.Grey.Lighten1)
                        .Border(0.2f).BorderColor(Colors.Grey.Darken3)
                        .PaddingVertical(2)
                        .PaddingHorizontal(6)
                        .AlignCenter();

                    static IContainer Td(IContainer x) => x
                        .Border(0.2f).BorderColor(Colors.Grey.Darken3)
                        .PaddingVertical(2)
                        .PaddingHorizontal(6);

                    static string Money(decimal? v) => (v ?? 0m).ToString("#,0");
                    static string MoneyPrice(decimal? v) => (v ?? 0m).ToString("#,0.0000");
                    static string Qty(decimal? v) => (v ?? 0m).ToString("#,0.00");

                    table.Header(row =>
                    {
                        Th(row.Cell()).Text("Tên SP\nProduct Name").FontSize(9).Bold();
                        Th(row.Cell()).Text("Số lượng\nQuantity (kg)").FontSize(9).Bold();
                        Th(row.Cell()).Text("Đơn giá\nPrice (VND)").FontSize(9).Bold();
                        Th(row.Cell()).Text("Đóng bao\nPackage").FontSize(9).Bold();
                        Th(row.Cell()).Text("Thành tiền\nTotal (VND)").FontSize(9).Bold();
                    });

                    if (templateOnly)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            Td(table.Cell()).Text("");
                            Td(table.Cell()).Text("");
                            Td(table.Cell()).Text("");
                            Td(table.Cell()).Text("");
                            Td(table.Cell()).Text("");
                        }

                        Td(table.Cell().ColumnSpan(4)).AlignCenter().Text(t =>
                        {
                            t.Span("Thành tiền/Total").Bold();
                            t.Span("\n(Chưa thuế GTGT/Without VAT)");
                        });
                        Td(table.Cell()).AlignRight().Text("").Bold();

                        Td(table.Cell().ColumnSpan(4)).AlignCenter().Text("Thuế GTGT/VAT (%)");
                        Td(table.Cell()).AlignRight().Text("").Bold();

                        Td(table.Cell().ColumnSpan(4)).AlignCenter().Text("Tổng cộng/Total").Bold();
                        Td(table.Cell()).AlignRight().Text("").Bold();
                    }
                    else
                    {
                        foreach (var it in details)
                        {
                            var qty = it.RequestQuantity ?? 0m;
                            var price = it.UnitPriceAgreed ?? 0m;
                            var line = it.TotalUnitPriceAgreed ?? 0m;

                            Td(table.Cell()).AlignLeft().Text(it.MaterialNameSnapshot ?? "").FontSize(9);
                            Td(table.Cell()).AlignRight().Text(Qty(qty)).FontSize(9);
                            Td(table.Cell()).AlignRight().Text(MoneyPrice(price)).FontSize(9);
                            Td(table.Cell()).AlignRight().Text(it.Package ?? "").FontSize(9);
                            Td(table.Cell()).AlignRight().Text(Money(line)).FontSize(9);
                        }

                        var (subTotal, vatAmt, grand) = CalculateTotals(d);

                        Td(table.Cell().ColumnSpan(4)).AlignCenter().Text(t =>
                        {
                            t.Span("Thành tiền/Total").Bold();
                            t.Span("\n(Chưa thuế GTGT/Without VAT)");
                        });
                        Td(table.Cell()).AlignRight().Text(Money(subTotal)).Bold();

                        Td(table.Cell().ColumnSpan(4)).AlignCenter().Text($"Thuế GTGT/VAT ({d.Vat ?? 0:0}%)");
                        Td(table.Cell()).AlignRight().Text(Money(vatAmt)).Bold();

                        Td(table.Cell().ColumnSpan(4)).AlignCenter().Text("Tổng cộng/Total").Bold();
                        Td(table.Cell()).AlignRight().Text(Money(grand)).Bold();
                    }
                });

                col.Item().PaddingTop(6).Text(txt =>
                {
                    txt.Span("Ghi chú (Note): ").Bold();
                    txt.Span(templateOnly ? "" : d.Note ?? "");
                });

                col.Item().PaddingTop(6).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(14);
                        c.RelativeColumn();
                    });

                    static IContainer Num(IContainer x) => x.PaddingRight(4).AlignRight();
                    static IContainer Txt(IContainer x) => x.PaddingBottom(2);

                    void Row(string no, Action<IContainer> content)
                    {
                        Num(t.Cell()).Text(no + ".");
                        content(t.Cell());
                    }

                    t.Cell().ColumnSpan(2)
                        .PaddingBottom(4)
                        .Text(txt =>
                        {
                            txt.Span("Các điều khoản khác/Other conditions:").SemiBold().Underline();
                        });

                    Row("1", c => Txt(c).Text(txt =>
                    {
                        txt.Span("Thời hạn giao hàng/Delivery Date: ").SemiBold();
                        txt.Span(templateOnly ? "....../....../......" : d.RequestDeliveryDate?.ToString("dd/MM/yyyy"));
                    }));

                    Row("2", c => Txt(c).Text(txt =>
                    {
                        txt.Span("Địa điểm giao/Delivery Address: ").SemiBold();
                        txt.Span(templateOnly ? "" : d.DeliveryAddress);
                    }));

                    Row("3", c => Txt(c).Text(txt =>
                    {
                        txt.Span("Thanh toán/Payment Term: ").SemiBold();
                        txt.Span(templateOnly ? "" : d.PaymentTypes);
                    }));

                    Row("4", c => Txt(c).Text(txt =>
                    {
                        txt.Span("Các chứng từ bắt buộc phải có: ").SemiBold();
                        txt.Span("Phiếu xuất kho (phiếu giao hàng); Hóa đơn (bản chính hoặc photo); COA; Đơn đặt hàng có xác nhận của nhà cung cấp. Nếu thiếu một trong những chứng từ trên, BP Kho có quyền không nhận hàng.\n");
                        txt.Span("The documents must have when you deliver: Delivery order; Invoice (original or photo); COA; PO’s confirm. If one of the above documents is missing, our warehouse has the right not to receive goods.");
                    }));
                });

                col.Item().PaddingTop(6).Text(txt =>
                {
                    txt.Span("Thông tin xuất hóa đơn/ Tax invoice information:\n").SemiBold().Underline();
                    txt.Span("CÔNG TY TNHH CƠ KHÍ NHỰA VIỆT ÚC\n").FontSize(10).Bold();
                    txt.Span("26/6 Đường 12, Phường Tam Bình, Thành Phố Hồ Chí Minh, Việt Nam\n").FontSize(10).Bold();
                    txt.Span("Mã số thuế/ Tax code: 0312889634\n").FontSize(10).Bold();
                    txt.Span("Điện thoại/ Tel: 028-7309.3969\n").FontSize(10).Bold();
                    txt.Span("Số tài khoản/ Account Number: 0371000473451 tại NH Vietcombank – CN Tân Định").FontSize(10).Bold();
                });

                col.Item().PaddingTop(14).ExtendHorizontal().Row(r =>
                {
                    r.Spacing(0);

                    r.RelativeItem(1).Column(c1 =>
                    {
                        c1.Item().AlignLeft().Text(t =>
                        {
                            t.Span("Trân trọng kính chào").SemiBold();
                            t.Span(" (").SemiBold();
                            t.Span("Best regards").SemiBold();
                            t.Span(")\n").SemiBold();

                            t.Span(templateOnly ? "" : "Nguyễn Văn Phương\n").Bold();
                            t.Span(templateOnly ? "" : "Director").SemiBold();
                        });
                    });

                    r.RelativeItem(1).Column(c2 =>
                    {
                        c2.Item().AlignRight().Text(t =>
                        {
                            t.Span("Xác nhận nhà cung cấp").SemiBold();
                            t.Span(" (").SemiBold();
                            t.Span("Supplier confirmation").SemiBold();
                            t.Span(")").SemiBold();
                        });

                        c2.Item().Height(60);
                        c2.Item().AlignRight().Text(templateOnly ? "" : "-----------------------------").FontSize(9);
                    });
                });
            });
        }
    }
}
