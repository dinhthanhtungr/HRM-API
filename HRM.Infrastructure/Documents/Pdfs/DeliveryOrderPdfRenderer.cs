using System.Globalization;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using Microsoft.Extensions.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs;

/// <summary>Renders the legacy VA-WH-F18(05) form with all selected lots in each detail row.</summary>
internal sealed class DeliveryOrderPdfRenderer(IHostEnvironment? environment = null) : IDeliveryOrderPdfRenderer
{
    private readonly string _assetRoot = Path.Combine(environment?.ContentRootPath ?? AppContext.BaseDirectory, "Assets", "Pdf");

    public byte[] Render(DeliveryOrderDocumentDto model)
    {
        var logo = LoadImage("VietAusLogo.png");
        var bureau = LoadImage("bureau-veritas.png");
        var grs = LoadImage("GRS.png");
        var qr = LoadImage("QR.png");
        // Legacy labels the export date Create Date and the stored date Delivery date.
        var printDate = DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(12);
            page.DefaultTextStyle(x => x.FontFamily(PdfTypography.FontFamily).FontSize(9));
            page.Header().Element(container => ComposeHeader(container, logo));
            page.Content().Element(container => ComposeContent(container, model, printDate));
            page.Footer().Element(container => ComposeFooter(container, bureau, grs, qr));
        })).GeneratePdf();
    }

    private byte[]? LoadImage(string filename)
    {
        var path = Path.Combine(_assetRoot, filename);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    private static void ComposeHeader(IContainer container, byte[]? logo)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Height(60).AlignMiddle().AlignLeft().Element(image =>
                {
                    if (logo is { Length: > 0 }) image.Image(logo).FitHeight();
                    else image.Text("Logo").FontSize(10);
                });
                row.RelativeItem().AlignMiddle().Border(1).PaddingLeft(5).PaddingBottom(2).PaddingTop(2).Column(info =>
                {
                    InformationRow(info, "Factory 01:", "No.296, Trung Thang, Dong Hoa ward,\nHo Chi Minh city, Vietnam.");
                    InformationRow(info, "Factory 02:", "Industrial park slope 47, Long Khanh 2 quarter,\nTam Phuoc ward, Dong Nai city, Vietnam.");
                    InformationRow(info, "Tel:", "(84). 28. 730 93 969");
                    InformationRow(info, "Website: ", "https://vietaus.com", Colors.Blue.Medium);
                });
            });
            column.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);
        });
    }

    private static void InformationRow(ColumnDescriptor column, string label, string value, string? color = null)
    {
        column.Item().Row(row =>
        {
            var labelText = row.RelativeItem(1).Text(label).FontSize(7);
            var valueText = row.RelativeItem(3).Text(value).FontSize(7);
            if (color is not null)
            {
                labelText.FontColor(color);
                valueText.FontColor(color);
            }
        });
    }

    private static void ComposeContent(IContainer container, DeliveryOrderDocumentDto model, string printDate)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().PaddingTop(5).Text("PHIẾU GIAO HÀNG / DELIVERY ORDER").FontSize(14).Bold();
            column.Item().PaddingTop(5).AlignRight().Row(row =>
            {
                row.Spacing(25);
                row.AutoItem().Text($"No: {model.ExternalId}");
                row.AutoItem().Text($"Ngày tạo/Create Date: {printDate}");
            });
            column.Item().PaddingBottom(5).Table(table =>
            {
                table.ColumnsDefinition(columns => { columns.ConstantColumn(180); columns.RelativeColumn(); });
                void AddRow(string label, string? value)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(label).FontSize(9).SemiBold();
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(value ?? string.Empty).FontSize(9);
                }
                AddRow("Đơn hàng (Product order):", model.MerchandiseOrderExternalIds);
                AddRow("Họ tên người mua (Customer name):", model.Receiver);
                AddRow("Đơn vị (Company name):", model.CustomerName);
                AddRow("Địa chỉ (Address):", model.CustomerAddress);
                AddRow("Hình thức thanh toán (Payment term):", model.PaymentType);
                AddRow("Mã số thuế (VAT code):", model.TaxNumber);
                AddRow("Ngày giao (Delivery date):", $"{model.CreatedDate:dd/MM/yyyy}");
                AddRow("Người giao (Deliverer):", string.Join(", ", model.Deliverers));
            });
            column.Item().PaddingTop(10).PaddingBottom(10).Table(table => ComposeLines(table, model));
            column.Item().PaddingTop(6).Text(text =>
            {
                text.Span("Ghi chú (Note): ").Bold();
                text.Span(model.Note ?? string.Empty);
            });
            column.Item().PaddingTop(6).Text(text =>
            {
                text.Span("Địa chỉ giao hàng (Delivery Address): ").Bold();
                text.Span(model.DeliveryAddress ?? string.Empty);
            });
            column.Item().Text(text =>
            {
                text.Span("Liên hệ nhận hàng (Contact person): ").Bold();
                text.Span(model.Receiver ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(model.Phone)) text.Span($" ({model.Phone.Trim()})").Italic();
            });
            column.Item().PaddingTop(14).Row(row =>
            {
                Signature(row, "Người lập phiếu", "Preparer");
                Signature(row, "Thủ kho", "Storekeeper");
                Signature(row, "Người nhận hàng", "Receiver");
            });
        });
    }

    private static void ComposeLines(TableDescriptor table, DeliveryOrderDocumentDto model)
    {
        table.ColumnsDefinition(columns =>
        {
            foreach (var weight in new[] { 1.2f, 4.3f, 1.5f, 0.9f, 1.4f, 1.0f, 1.8f }) columns.RelativeColumn(weight);
        });
        table.Header(header =>
        {
            foreach (var label in new[] { "Mã SP\nCode", "Tên SP\nProduct Name", "Số lô\nBatch No", "Đơn vị\nUnit", "Số lượng\nQuantity", "Số bao\nBag No", "Số PO\nPO No" })
                header.Cell().Border(1).BorderColor(Colors.Black).Background(Colors.Grey.Lighten1).AlignCenter().Text(label).FontSize(9).Bold();
        });
        foreach (var group in model.Lines.GroupBy(line => line.ProductCode).OrderByDescending(group => group.Key))
        foreach (var line in group)
        {
            Cell(table.Cell(), line.ProductCode, horizontalPadding: 4);
            table.Cell().Border(1).BorderColor(Colors.Black).PaddingVertical(4).PaddingHorizontal(6)
                .AlignMiddle().AlignCenter().Text(line.ProductName ?? string.Empty);
            // The query joins all active consumption lots without multiplying quantity by lot count.
            Cell(table.Cell(), line.LotNo);
            Cell(table.Cell(), "Kg");
            var quantity = table.Cell().Border(1).BorderColor(Colors.Black);
            if (line.IsSingleMaterialFormula) quantity = quantity.Background(Colors.Grey.Lighten3);
            quantity.PaddingVertical(4).PaddingHorizontal(6).AlignRight().Text(line.Quantity.ToString("0.###", CultureInfo.InvariantCulture));
            Cell(table.Cell(), line.NumOfBags.ToString(CultureInfo.InvariantCulture), right: true);
            Cell(table.Cell(), line.PONo);
        }
        // Legacy PDF totals all printed details, including attached goods. Excel retains its existing totals.
        table.Cell().ColumnSpan(7).Border(1).BorderColor(Colors.Black).PaddingVertical(4).PaddingHorizontal(6).AlignRight().Text(text =>
        {
            text.Span($"Tổng cộng: {model.Lines.Sum(line => line.Quantity):0.00} Kg - {model.Lines.Sum(line => line.NumOfBags)} Bao").Bold();
            text.Span(" (Total)").Bold();
        });
    }

    private static void Cell(IContainer container, string? value, bool right = false, float horizontalPadding = 6)
    {
        var cell = container.Border(1).BorderColor(Colors.Black).PaddingVertical(4).PaddingHorizontal(horizontalPadding);
        if (right) cell.AlignRight().Text(value ?? string.Empty);
        else cell.AlignLeft().Text(value ?? string.Empty);
    }

    private static void Signature(RowDescriptor row, string vietnamese, string english)
    {
        row.RelativeItem().Column(column =>
        {
            column.Item().AlignCenter().Text($"{vietnamese} ({english})").SemiBold();
            column.Item().AlignCenter().Text("(Tên, chữ ký/Fullname, signature)").Italic().FontSize(9);
            column.Item().MinHeight(60);
        });
    }

    private static void ComposeFooter(IContainer container, byte[]? bureau, byte[]? grs, byte[]? qr)
    {
        container.PaddingTop(20).Column(column =>
        {
            column.Item().PaddingTop(10).Row(row => row.RelativeItem().AlignCenter().AlignMiddle().Column(contact =>
            {
                contact.Item().AlignCenter().AlignMiddle().Text(text =>
                {
                    text.Span("Website: ");
                    text.Span("https://vietaus.com").FontColor(Colors.Blue.Medium);
                    text.Span(" - hotline: (84). 8. 73 09 39 69");
                });
                contact.Item().AlignCenter().AlignMiddle().Text("COLOURING YOUR FUTURE WITH SERVICE AT YOUR DOORSTEP").Bold().FontSize(9);
            }));
            column.Item().PaddingTop(10).Row(row => row.RelativeItem().AlignCenter().AlignMiddle().Row(images =>
            {
                CertificationImage(images, bureau, 80);
                CertificationImage(images, grs, 80);
                CertificationImage(images, qr, 45);
            }));
            column.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text("VA-WH-F18(05)").FontSize(9);
                row.RelativeItem().AlignRight().Text("14-07-2026").FontSize(9);
            });
        });
    }

    private static void CertificationImage(RowDescriptor row, byte[]? image, float width)
    {
        if (image is { Length: > 0 }) row.ConstantItem(width).PaddingRight(10).AlignMiddle().Image(image).FitWidth();
    }
}
