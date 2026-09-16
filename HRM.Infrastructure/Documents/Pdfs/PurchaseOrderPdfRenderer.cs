using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs;

internal sealed class PurchaseOrderPdfRenderer : IPurchaseOrderPdfRenderer
{
    public byte[] Render(PurchaseOrderDetailDto purchaseOrder) => Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Header().Column(column =>
            {
                column.Item().AlignCenter().Text("ĐƠN ĐẶT HÀNG / PURCHASE ORDER").Bold().FontSize(16);
                column.Item().AlignCenter().Text(purchaseOrder.ExternalId).FontSize(12);
            });
            page.Content().PaddingVertical(16).Column(column =>
            {
                column.Spacing(8);
                column.Item().Text($"Nhà cung cấp: {purchaseOrder.SupplierName}");
                column.Item().Text($"Ngày tạo: {purchaseOrder.CreateDate:dd/MM/yyyy}    Ngày giao dự kiến: {purchaseOrder.RequestDeliveryDate:dd/MM/yyyy}");
                column.Item().Text($"Địa chỉ giao: {purchaseOrder.DeliveryAddress ?? "-"}");
                column.Item().Text($"Thanh toán: {purchaseOrder.PaymentTypes ?? "-"}    VAT: {purchaseOrder.Vat ?? 0}%");
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(28);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });
                    table.Header(header =>
                    {
                        Cell(header.Cell(), "STT"); Cell(header.Cell(), "Mã"); Cell(header.Cell(), "Tên vật tư");
                        Cell(header.Cell(), "SL"); Cell(header.Cell(), "Đơn giá"); Cell(header.Cell(), "Thành tiền");
                    });
                    foreach (var line in purchaseOrder.Items)
                    {
                        Cell(table.Cell(), line.LineNo.ToString());
                        Cell(table.Cell(), line.MaterialCode);
                        Cell(table.Cell(), line.MaterialName);
                        Cell(table.Cell(), line.Quantity.ToString("N2"));
                        Cell(table.Cell(), line.UnitPriceAgreed.ToString("N0"));
                        Cell(table.Cell(), line.TotalPriceAgreed.ToString("N0"));
                    }
                });
                column.Item().AlignRight().Text($"Tổng cộng: {purchaseOrder.TotalPrice:N0}").Bold();
                if (!string.IsNullOrWhiteSpace(purchaseOrder.Comment))
                    column.Item().Text($"Ghi chú: {purchaseOrder.Comment}");
                if (!string.IsNullOrWhiteSpace(purchaseOrder.PlpuComment))
                    column.Item().Text($"Ghi chú PL/PU: {purchaseOrder.PlpuComment}");
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Trang ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }).GeneratePdf();

    private static void Cell(IContainer container, string text) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(text);
}
