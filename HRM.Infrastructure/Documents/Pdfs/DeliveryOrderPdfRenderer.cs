using System.Globalization;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs;

internal sealed class DeliveryOrderPdfRenderer : IDeliveryOrderPdfRenderer
{
    public byte[] Render(DeliveryOrderDocumentDto model) => Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontFamily(PdfTypography.FontFamily).FontSize(9));
            page.Header().Column(column =>
            {
                column.Item().Text(model.CompanyName).Bold().FontSize(12);
                column.Item().Text(model.CompanyAddress ?? string.Empty).FontSize(8);
                column.Item().PaddingTop(10).AlignCenter().Text("PHIẾU GIAO HÀNG / DELIVERY ORDER").Bold().FontSize(15);
                column.Item().PaddingTop(5).Text($"Số: {model.ExternalId ?? model.Id.ToString()}     Ngày tạo: {model.CreatedDate:dd/MM/yyyy}");
                column.Item().Text($"Trạng thái: {model.Status}").Bold();
            });
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(5);
                column.Item().Text($"Khách hàng: {model.CustomerName}");
                column.Item().Text($"Người nhận: {model.Receiver}    Điện thoại: {model.Phone}");
                column.Item().Text($"Địa chỉ giao: {model.DeliveryAddress}");
                column.Item().Text($"Mã số thuế: {model.TaxNumber}    Thanh toán: {model.PaymentType}");
                column.Item().Text($"Người giao: {string.Join(", ", model.Deliverers)}");
                column.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.5f); c.RelativeColumn(3); c.RelativeColumn(2);
                        c.RelativeColumn(1.3f); c.RelativeColumn(1); c.RelativeColumn(1.8f);
                    });
                    table.Header(h =>
                    {
                        foreach (var label in new[] { "Mã SP", "Tên sản phẩm", "Lot / Batch", "SL (kg)", "Số bao", "PO" })
                            Cell(h.Cell().Background(Colors.Grey.Lighten3), label, true);
                    });
                    foreach (var line in model.Lines)
                    {
                        Cell(table.Cell(), line.ProductCode);
                        Cell(table.Cell(), (line.IsAttach ? "[Kèm] " : string.Empty) + line.ProductName);
                        Cell(table.Cell(), line.LotNo);
                        Cell(table.Cell(), line.Quantity.ToString("#,##0.###", CultureInfo.GetCultureInfo("vi-VN")), right: true);
                        Cell(table.Cell(), line.NumOfBags.ToString(CultureInfo.InvariantCulture), right: true);
                        Cell(table.Cell(), line.PONo);
                    }
                });
                column.Item().AlignRight().Text($"Tổng: {model.TotalQuantity.ToString("#,##0.###", CultureInfo.GetCultureInfo("vi-VN"))} kg / {model.TotalBags} bao").Bold();
                column.Item().Text("Tổng không bao gồm dòng hàng kèm. Ngày tạo không phải ngày xác nhận giao.").FontSize(7);
                if (!string.IsNullOrWhiteSpace(model.Note)) column.Item().Text($"Ghi chú: {model.Note}");
                column.Item().PaddingTop(18).Row(row =>
                {
                    foreach (var label in new[] { "Người lập phiếu", "Người giao hàng", "Người nhận hàng" })
                        row.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().Text(label).Bold();
                            c.Item().Text("(Ký, ghi rõ họ tên)").FontSize(8);
                            c.Item().Height(45);
                        });
                });
            });
            page.Footer().AlignCenter().Text(t => { t.Span("Trang "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
        });
    }).GeneratePdf();

    private static void Cell(IContainer container, string? value, bool bold = false, bool right = false)
    {
        var content = container.EnsureSpace(45).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4);
        if (right) content = content.AlignRight();
        var text = content.Text(value ?? string.Empty);
        if (bold) text.Bold();
    }
}
