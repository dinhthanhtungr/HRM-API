using System.Globalization;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs;

internal sealed class ManufacturingVUFormulaPdfRenderer
    : IManufacturingVUFormulaPdfRenderer
{
    private const string DocumentCode = "VA-PL&PU-F02(04)";
    private const string TemplateRevisionDate = "25-03-2026";

    public byte[] Render(ManufacturingVUFormulaPdfDocumentDto document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(10);
                page.DefaultTextStyle(text => text.FontFamily("Open Sans").FontSize(9));
                page.Content().Element(content => ComposeContent(content, document));
                page.Footer().Component(new ManufacturingFooterComponent());
            });
        }).GeneratePdf();
    }

    private static void ComposeContent(
        IContainer container,
        ManufacturingVUFormulaPdfDocumentDto document)
    {
        container.Column(column =>
        {
            column.Item()
                .AlignCenter()
                .PaddingTop(5)
                .Text("LỆNH SẢN XUẤT")
                .FontSize(14)
                .Bold();

            column.Item().PaddingTop(5).AlignRight().Row(row =>
            {
                row.Spacing(25);
                row.AutoItem().Text(text =>
                {
                    text.Span("VU No: ").FontSize(9);
                    text.Span(ValueOrDash(document.FormulaExternalId)).FontSize(11).Bold();
                    text.Span(" - Batch#: ").FontSize(9);
                    text.Span(ValueOrDash(document.FormulaExternalId)).FontSize(11).Bold();
                });
                row.AutoItem().Text(text =>
                {
                    text.Span("Ngày in: ").FontSize(9);
                    text.Span(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(9).Bold();
                });
            });

            column.Item().PaddingBottom(5).Element(
                content => ComposeOrderInformation(content, document));
            column.Item().PaddingTop(10).Element(
                content => ComposeMaterialsTable(content, document));
            column.Item().PaddingTop(10).Element(
                content => ComposeMachineSetup(content, document));
        });
    }

    private static void ComposeOrderInformation(
        IContainer container,
        ManufacturingVUFormulaPdfDocumentDto document)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.2f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.2f);
                });

                var perBatch = document.NumOfBatches > 0
                    ? document.TotalProductionQuantity / document.NumOfBatches
                    : 0m;

                AddInformationCell(table, "Khách hàng: ", document.CustomerCode);
                AddInformationCell(table, "Colour Code: ", document.ColourCode);
                AddInformationCell(
                    table,
                    "Khối lượng/mẻ (Kg): ",
                    document.NumOfBatches > 0 ? FormatNumber(perBatch, 2) : null);

                AddInformationCell(
                    table,
                    "Tên sp: ",
                    document.ProductName ?? document.FormulaName);
                AddInformationCell(table, "Kiểm tra: ", document.QcCheck);
                AddInformationCell(table, "Số mẻ: ", document.NumOfBatches.ToString());

                AddInformationCell(
                    table,
                    "Ngày yêu cầu: ",
                    document.RequestDate?.ToString("dd/MM/yyyy"));
                AddInformationCell(
                    table,
                    "Tỷ lệ sử dụng: ",
                    document.UsageRate.HasValue
                        ? $"{document.UsageRate.Value.ToString("0.##", CultureInfo.InvariantCulture)} %"
                        : null);
                AddInformationCell(
                    table,
                    "Tổng khối lượng (kg): ",
                    FormatNumber(document.TotalProductionQuantity, 2));
            });

            if (!string.IsNullOrWhiteSpace(document.LabNote) ||
                !string.IsNullOrWhiteSpace(document.Requirement))
            {
                column.Item()
                    .PaddingTop(6)
                    .Element(content => ComposeNotes(
                        content,
                        document.LabNote,
                        document.Requirement));
            }
        });
    }

    private static void AddInformationCell(
        TableDescriptor table,
        string label,
        string? value)
    {
        table.Cell().Element(InformationCell).Text(text =>
        {
            text.Span(label).FontSize(8);
            text.Span(ValueOrDash(value)).FontSize(10).Bold();
        });
    }

    private static IContainer InformationCell(IContainer container)
        => container
            .Border(1)
            .BorderColor(Colors.Black)
            .PaddingVertical(2)
            .PaddingHorizontal(4)
            .AlignMiddle();

    private static void ComposeNotes(
        IContainer container,
        string? labNote,
        string? requirement)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Cell().Element(NoteHeaderCell).Text("Ghi chú thực hiện:").SemiBold();
            table.Cell().Element(NoteHeaderCell).Text("Yêu cầu ca SX và QC thực hiện:").SemiBold();
            table.Cell().Element(NoteBodyCell).Text(ValueOrDash(labNote)).FontSize(9);
            table.Cell().Element(NoteBodyCell).Text(ValueOrDash(requirement)).FontSize(9);
        });
    }

    private static IContainer NoteHeaderCell(IContainer container)
        => container
            .Border(1)
            .BorderColor(Colors.Black)
            .Background(Colors.Grey.Lighten2)
            .PaddingVertical(4)
            .PaddingHorizontal(6);

    private static IContainer NoteBodyCell(IContainer container)
        => container
            .Border(1)
            .BorderColor(Colors.Black)
            .MinHeight(35)
            .PaddingVertical(4)
            .PaddingHorizontal(6)
            .AlignTop();

    private static void ComposeMaterialsTable(
        IContainer container,
        ManufacturingVUFormulaPdfDocumentDto document)
    {
        var materials = document.Materials
            .Where(x => x.Quantity > 0)
            .OrderBy(x => CategorySortKey(x.CategoryName))
            .ThenBy(x => x.LineNo)
            .ToList();

        if (materials.Count == 0)
        {
            container.Text("Không có nguyên vật liệu.").Italic();
            return;
        }

        var batchCount = Math.Max(document.NumOfBatches, 1);
        var perBatchQuantity = document.TotalProductionQuantity / batchCount;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.50f);
                columns.RelativeColumn(3.40f);
                columns.RelativeColumn(1.10f);
                columns.RelativeColumn(1.20f);
                columns.RelativeColumn(1.35f);
                columns.RelativeColumn(1.25f);
                columns.RelativeColumn(1.45f);
            });

            table.Header(header =>
            {
                AddMaterialHeader(header, "Code");
                AddMaterialHeader(header, "Name");
                AddMaterialHeader(header, "Lot #");
                AddMaterialHeader(header, "Std");
                AddMaterialHeader(header, "SL / mẻ (kg)");
                AddMaterialHeader(header, "SL / mẻ (g)");
                AddMaterialHeader(header, "SL tổng (kg)");
            });

            decimal sumStandard = 0;
            decimal sumPerBatch = 0;
            decimal sumTotal = 0;

            foreach (var group in materials.GroupBy(x => ValueOrOther(x.CategoryName)))
            {
                table.Cell()
                    .ColumnSpan(7)
                    .Element(GroupTitleCell)
                    .AlignCenter()
                    .Text(group.Key)
                    .SemiBold();

                foreach (var material in group)
                {
                    var standard = material.Quantity;
                    var materialPerBatch = standard * perBatchQuantity;
                    var materialTotal = standard * document.TotalProductionQuantity;

                    sumStandard += standard;
                    sumPerBatch += materialPerBatch;
                    sumTotal += materialTotal;

                    table.Cell().Element(MaterialBodyCell)
                        .Text(ValueOrDash(material.MaterialCode)).FontSize(10).Bold();
                    table.Cell().Element(MaterialBodyCell)
                        .Text(ValueOrDash(material.MaterialName)).FontSize(10).Bold();
                    table.Cell().Element(MaterialBodyCell)
                        .Text(ValueOrDash(material.LotNo)).FontSize(8).Bold();
                    table.Cell().Element(MaterialBodyCell).AlignRight()
                        .Text(FormatNumber(standard, 7)).FontSize(10).Bold();
                    table.Cell().Element(MaterialBodyCell).AlignRight()
                        .Text(FormatNumber(materialPerBatch, 7)).FontSize(10).Bold();
                    table.Cell().Element(MaterialBodyCell).AlignRight()
                        .Text(FormatNumber(materialPerBatch * 1_000m, 4)).FontSize(10).Bold();
                    table.Cell().Element(MaterialBodyCell).AlignRight()
                        .Text(FormatNumber(materialTotal, 7)).FontSize(10).Bold();
                }
            }

            table.Cell().ColumnSpan(3).Element(MaterialTotalCell)
                .AlignCenter().Text("Tổng").SemiBold();
            table.Cell().Element(MaterialTotalCell).AlignRight()
                .Text(FormatNumber(sumStandard, 7)).Bold();
            table.Cell().Element(MaterialTotalCell).AlignRight()
                .Text(FormatNumber(sumPerBatch, 7)).Bold();
            table.Cell().Element(MaterialTotalCell).AlignRight()
                .Text(FormatNumber(sumPerBatch * 1_000m, 4)).Bold();
            table.Cell().Element(MaterialTotalCell).AlignRight()
                .Text(FormatNumber(sumTotal, 7)).Bold();
        });
    }

    private static void AddMaterialHeader(TableCellDescriptor header, string value)
        => header.Cell()
            .Element(MaterialHeaderCell)
            .AlignMiddle()
            .Text(value)
            .FontSize(9)
            .Bold();

    private static IContainer MaterialHeaderCell(IContainer container)
        => container
            .Border(0.5f)
            .BorderColor(Colors.Black)
            .Background(Colors.Grey.Lighten2)
            .PaddingVertical(2)
            .PaddingHorizontal(2);

    private static IContainer GroupTitleCell(IContainer container)
        => container
            .Border(0.5f)
            .BorderColor(Colors.Black)
            .PaddingVertical(2)
            .PaddingHorizontal(2);

    private static IContainer MaterialBodyCell(IContainer container)
        => container
            .BorderLeft(0.5f)
            .BorderRight(0.5f)
            .BorderBottom(0.5f)
            .BorderColor(Colors.Black)
            .PaddingVertical(2)
            .PaddingHorizontal(4);

    private static IContainer MaterialTotalCell(IContainer container)
        => MaterialBodyCell(container);

    private static void ComposeMachineSetup(
        IContainer container,
        ManufacturingVUFormulaPdfDocumentDto document)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(55);
                for (var index = 0; index < 7; index++)
                {
                    columns.RelativeColumn();
                }
            });

            table.Cell().ColumnSpan(8).Element(MachineTitleCell)
                .AlignCenter()
                .Text("Thông số cài đặt (yêu cầu trưởng ca SX phải cập nhật tốc độ, nhiệt độ máy đùn thực tế lúc máy đang SX)")
                .FontSize(9)
                .SemiBold();

            AddMachineHeaderRow(
                table,
                "",
                "Operator",
                "Machine",
                "Mfg Date",
                "Batch #",
                "Total Qty (kg)",
                "Time (h)",
                "Prod (kgs/h)");
            AddMachineBodyRow(
                table,
                "Previous",
                "",
                "",
                "",
                document.FormulaExternalId,
                "",
                "",
                "");
            AddMachineHeaderRow(
                table,
                "",
                "Rpm",
                "Zone 1(°C)",
                "Zone 2(°C)",
                "Zone 3(°C)",
                "Zone 4(°C)",
                "Zone 5(°C)",
                "Zone 6(°C)");
            AddMachineBodyRow(table, "Previous", "", "", "", "", "", "", "");
            AddMachineHeaderRow(
                table,
                "",
                "Zone 7(°C)",
                "Zone 8(°C)",
                "Zone 9(°C)",
                "Zone 10(°C)",
                "Zone 11(°C)",
                "Die-head(°C)",
                "");
            AddMachineBodyRow(table, "Previous", "", "", "", "", "", "", "");

            table.Cell().ColumnSpan(8).Element(MachineBodyCell)
                .Text("Note/Lưu ý: tolerance temperature permitted up - down 10°C/nhiệt độ cài đặt được phép tăng giảm trong khoảng 10°C")
                .FontSize(8);

            table.Cell().ColumnSpan(8).Element(ComposeSignatureSection);
        });
    }

    private static void AddMachineHeaderRow(TableDescriptor table, params string[] values)
    {
        foreach (var value in values)
        {
            table.Cell().Element(MachineHeaderCell).Text(value).SemiBold();
        }
    }

    private static void AddMachineBodyRow(TableDescriptor table, params string[] values)
    {
        foreach (var value in values)
        {
            table.Cell().Element(MachineBodyCell).Text(value).FontSize(8);
        }
    }

    private static IContainer MachineTitleCell(IContainer container)
        => container
            .Border(1)
            .BorderColor(Colors.Black)
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(4)
            .PaddingHorizontal(4);

    private static IContainer MachineHeaderCell(IContainer container)
        => container
            .Border(1)
            .BorderColor(Colors.Black)
            .Background(Colors.Grey.Lighten2)
            .PaddingVertical(3)
            .PaddingHorizontal(3)
            .AlignCenter();

    private static IContainer MachineBodyCell(IContainer container)
        => container
            .Border(1)
            .BorderColor(Colors.Black)
            .PaddingVertical(3)
            .PaddingHorizontal(3);

    private static void ComposeSignatureSection(IContainer container)
    {
        container
            .Border(1)
            .BorderColor(Colors.Black)
            .Column(column =>
            {
                column.Item().Row(row =>
                {
                    AddSignatureHeader(row, "Thành phẩm");
                    AddSignatureHeader(row, "Người tổng kết");
                    AddSignatureHeader(row, "Check by (Lab)");
                    AddSignatureHeader(row, "Approved");
                });
                column.Item().Height(50).Row(row =>
                {
                    AddSignatureBody(row, string.Empty);
                    AddSignatureBody(row, "Ký & ghi rõ họ tên");
                    AddSignatureBody(row, "Ký & ghi rõ họ tên");
                    AddSignatureBody(row, "Ký & ghi rõ họ tên");
                });
            });
    }

    private static void AddSignatureHeader(RowDescriptor row, string value)
        => row.RelativeItem()
            .BorderRight(1)
            .BorderColor(Colors.Black)
            .Background(Colors.Grey.Lighten2)
            .PaddingVertical(3)
            .PaddingHorizontal(4)
            .Text(value)
            .SemiBold();

    private static void AddSignatureBody(RowDescriptor row, string value)
        => row.RelativeItem()
            .BorderRight(1)
            .BorderTop(1)
            .BorderColor(Colors.Black)
            .Padding(6)
            .AlignCenter()
            .AlignTop()
            .Text(value);

    private static void ComposeFooter(IContainer container)
    {
        container.PaddingTop(10).Column(column =>
        {
            column.Item().AlignCenter().Text(text =>
            {
                text.Span("Website: ");
                text.Span("https://vietaus.com").FontColor(Colors.Blue.Medium);
                text.Span(" - hotline: (84). 8. 73 09 39 69");
            });
            column.Item().AlignCenter()
                .Text("COLOURING YOUR FUTURE WITH SERVICE AT YOUR DOORSTEP")
                .Bold()
                .FontSize(9);
            column.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(DocumentCode).FontSize(9);
                row.RelativeItem().AlignCenter().Text(text =>
                {
                    text.Span("Trang ");
                    text.CurrentPageNumber();
                    text.Span("/");
                    text.TotalPages();
                });
                row.RelativeItem().AlignRight().Text(TemplateRevisionDate).FontSize(9);
            });
        });
    }

    private sealed class ManufacturingFooterComponent : IComponent
    {
        public void Compose(IContainer container)
        {
            ComposeFooter(container);
        }
    }

    private static int CategorySortKey(string? categoryName)
    {
        var normalized = categoryName?.ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("pigment") || normalized.Contains("bột màu")) return 1;
        if (normalized.Contains("additive") || normalized.Contains("phụ gia")) return 2;
        if (normalized.Contains("polymer") || normalized.Contains("nhựa")) return 3;
        if (normalized.Contains("other") || normalized.Contains("khác")) return 4;
        return 99;
    }

    private static string ValueOrOther(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Others/Khác" : value.Trim();

    private static string ValueOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string FormatNumber(decimal value, int decimals)
    {
        var format = "#,##0" + (decimals > 0 ? "." + new string('0', decimals) : string.Empty);
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
}
