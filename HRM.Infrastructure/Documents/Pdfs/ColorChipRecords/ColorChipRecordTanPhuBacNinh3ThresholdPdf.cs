using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Globalization;
using System.IO;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords
{
    public class ColorChipRecordTanPhuBacNinh3ThresholdPdf : IColorChipRecordTanPhuBacNinh3ThresholdPdf
    {
        private const float ContentFontSize = 8.5f;
        private const float LabelFontSize = 7.5f;
        private const float CompactFontSize = 7.2f;
        private const float CompactLabelFontSize = 6.6f;
        private const float BorderWidth = 1f;

        public byte[] RenderTemplate()
        {
            return Render(new ColorChipRecordPdfModel(), true);
        }

        public byte[] Render(ColorChipRecordPdfModel model, bool templateOnly = false)
        {
            model ??= new ColorChipRecordPdfModel();

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(12);
                    page.DefaultTextStyle(x => x.FontFamily("Times New Roman").FontSize(ContentFontSize));

                    page.Content().Element(x => BuildContent(x, model, templateOnly));
                    page.Footer().PaddingTop(4).Element(x => BuildFooter(x, model, templateOnly));
                });
            });

            return doc.GeneratePdf();
        }

        private void BuildContent(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Column(col =>
            {
                col.Spacing(4);

                col.Item().Row(row =>
                {
                    row.Spacing(6);

                    row.ConstantItem(105)
                        .PaddingTop(20)
                        .AlignTop()
                        .Element(x => BuildLogoSectionCompact(x, m));

                    row.RelativeItem()
                        .AlignTop()
                        .Element(x => BuildMainHeaderCompact(x, m, templateOnly));

                    row.ConstantItem(175)
                        .PaddingTop(20)
                        .AlignTop()
                        .Element(x => BuildApprovalCompact(x, m, templateOnly));
                });
            });
        }

        private void BuildLogoSectionCompact(IContainer c, ColorChipRecordPdfModel m)
        {
            var logoType = ParseLogoType(m.LogoTypeText);
            var (imagePath, companyName, website) = GetLogoInfo(logoType);

            c.AlignTop()
             .AlignCenter()
             .Column(col =>
             {
                 col.Spacing(1);

                 col.Item().AlignCenter().Element(e =>
                 {
                     if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                         e.Height(45).Image(Image.FromFile(imagePath)).FitHeight();
                     else
                         e.Text("Logo").FontSize(ContentFontSize);
                 });

                 col.Item()
                    .AlignCenter()
                    .Text(companyName)
                    .FontSize(8)
                    .Bold();

                 col.Item()
                    .AlignCenter()
                    .Text(website)
                    .FontSize(6.5f);
             });
        }

        private void BuildMainHeaderCompact(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Column(col =>
            {
                col.Spacing(3);

                col.Item()
                    .AlignCenter()
                    .Text(string.IsNullOrWhiteSpace(m.Title) ? "COLOR CHIPS" : m.Title)
                    .FontSize(15)
                    .Bold();

                col.Item().Element(x => BuildInfoTableCompact(x, m, templateOnly));
            });
        }

        private void BuildInfoTableCompact(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            var actualSize = templateOnly ? "" : m.SizeText ?? "";

            var actualWeight = templateOnly
                ? ""
                : m.PelletWeightGram.HasValue && m.PelletWeightGram.Value > 0
                    ? m.PelletWeightGram.Value.ToString("0.##", CultureInfo.InvariantCulture)
                    : "";

            var actualElectrostatic = templateOnly
                ? ""
                : m.Electrostatic == true ? "Có" : "Không có";

            c.Column(col =>
            {
                col.Spacing(2);

                col.Item().Border(BorderWidth).BorderColor(Colors.Black).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(54);
                        columns.RelativeColumn(1.1f);
                        columns.ConstantColumn(38);
                        columns.RelativeColumn(0.9f);
                        columns.ConstantColumn(36);
                        columns.RelativeColumn(0.9f);
                    });

                    AddCompactCell(table, "BATCH", label: true);
                    AddCompactCell(table, templateOnly ? "SAMPLE" : m.BatchNo, center: true, bold: true);

                    AddCompactCell(table, "DATE", label: true);
                    AddCompactCell(table, templateOnly ? "" : FormatDate(DateTime.Now), center: true, bold: true);

                    AddCompactCell(table, "CODE", label: true);
                    AddCompactCell(table, templateOnly ? "" : m.Code, center: true, bold: true);

                    AddCompactCell(table, "CUSTOMER", label: true);
                    AddCompactCell(table, templateOnly ? "" : m.Customer, colSpan: 5, center: true, bold: true);

                    AddCompactCell(table, "COLOR", label: true);
                    AddCompactCell(table, templateOnly ? "" : m.Color, colSpan: 5, center: true, bold: true);

                    AddCompactCell(table, "ADD RATE", label: true);
                    AddCompactCell(table, templateOnly ? "" : m.AddRate, center: true, bold: true);

                    AddCompactCell(table, "RESIN", label: true);
                    AddCompactCell(table, templateOnly ? "" : m.Resin, center: true, bold: true);

                    AddCompactCell(table, "TEMP", label: true);
                    AddCompactCell(table, templateOnly ? "" : m.TemperatureLimit, center: true, bold: true);

                    AddCompactCell(table, "MACHINE", label: true);
                    AddCompactCell(table, templateOnly ? "" : m.Machine, colSpan: 5, center: true, bold: true);
                });

                col.Item().Border(BorderWidth).BorderColor(Colors.Black).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1f);
                        columns.RelativeColumn(1f);
                    });

                    AddCompactCell(table, "SPECIFICATION", label: true, center: true, bold: true);
                    AddCompactCell(table, "ACTUAL", label: true, center: true, bold: true);
                    AddCompactCell(table, "STANDARD", label: true, center: true, bold: true);

                    AddCompactCell(table, "Size - Diameter x Length (mm)", label: true);
                    AddCompactCell(table, actualSize, center: true, bold: true);
                    AddCompactCell(table, "1.8 * 3.4", center: true, bold: true);

                    AddCompactCell(table, "Weight - Hạt/gram", label: true);
                    AddCompactCell(table, actualWeight, center: true, bold: true);
                    AddCompactCell(table, "80-120", center: true, bold: true);

                    AddCompactCell(table, "Electrostatic", label: true);
                    AddCompactCell(table, actualElectrostatic, center: true, bold: true);
                    AddCompactCell(table, "Không có", center: true, bold: true);
                });
            });
        }

        private void AddCompactCell(
            TableDescriptor table,
            string? text,
            bool label = false,
            bool center = false,
            bool bold = false,
            uint colSpan = 1,
            float minHeight = 13f)
        {
            var cell = colSpan > 1
                ? table.Cell().ColumnSpan(colSpan)
                : table.Cell();

            cell
                .Border(BorderWidth)
                .BorderColor(Colors.Black)
                .Background(label ? Colors.Grey.Lighten4 : Colors.White)
                .MinHeight(minHeight)
                .PaddingVertical(1.5f)
                .PaddingHorizontal(3)
                .Element(inner =>
                {
                    var container = center
                        ? inner.AlignMiddle().AlignCenter()
                        : inner.AlignMiddle().AlignLeft();

                    var txt = container.Text(string.IsNullOrWhiteSpace(text) ? "" : text)
                        .FontSize(label ? CompactLabelFontSize : CompactFontSize);

                    if (label || bold)
                        txt.Bold();
                });
        }

        private void BuildApprovalCompact(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Border(BorderWidth)
             .BorderColor(Colors.Black)
             .Column(col =>
             {
                 col.Item()
                    .Background(Colors.Grey.Lighten2)
                    .BorderBottom(BorderWidth)
                    .BorderColor(Colors.Black)
                    .PaddingVertical(3)
                    .PaddingHorizontal(6)
                    .AlignCenter()
                    .Text("APPROVAL")
                    .FontSize(ContentFontSize)
                    .Bold();

                 col.Item()
                    .BorderBottom(BorderWidth)
                    .BorderColor(Colors.Black)
                    .MinHeight(34)
                    .Padding(4)
                    .AlignMiddle()
                    .AlignCenter()
                    .Text(templateOnly ? "" : GetApprovalText(m))
                    .FontSize(CompactLabelFontSize);

                 col.Item()
                    .BorderBottom(BorderWidth)
                    .BorderColor(Colors.Black)
                    .MinHeight(26)
                    .Padding(4)
                    .Text(templateOnly ? "" : m.PrintNote ?? "")
                    .FontSize(CompactLabelFontSize);

                 col.Item()
                    .BorderBottom(BorderWidth)
                    .BorderColor(Colors.Black)
                    .PaddingVertical(3)
                    .PaddingHorizontal(5)
                    .Text($"Prepared by: {(templateOnly ? "" : m.PreparedBy)}")
                    .FontSize(CompactLabelFontSize)
                    .Bold();

                 col.Item()
                    .MinHeight(26)
                    .PaddingVertical(3)
                    .PaddingHorizontal(5)
                    .Text("Signature:")
                    .FontSize(CompactLabelFontSize)
                    .Bold();
             });
        }

        private void BuildFooter(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            var leftText = templateOnly
                ? "LOWER"
                : string.IsNullOrWhiteSpace(m.LowerText) ? "LOWER" : m.LowerText;

            var centerText = templateOnly
                ? "STANDARD"
                : string.IsNullOrWhiteSpace(m.StandardText) ? "STANDARD" : m.StandardText;

            var rightText = templateOnly
                ? "UPPER"
                : string.IsNullOrWhiteSpace(m.UpperText) ? "UPPER" : m.UpperText;

            c.Column(col =>
            {
                col.Spacing(5);

                col.Item().Row(row =>
                {
                    row.RelativeItem().AlignCenter().Text(leftText).FontSize(14).Bold();
                    row.RelativeItem().AlignCenter().Text(centerText).FontSize(14).Bold();
                    row.RelativeItem().AlignCenter().Text(rightText).FontSize(14).Bold();
                });

                col.Item()
                    .PaddingLeft(28)
                    .Text(text =>
                    {
                        text.DefaultTextStyle(x => x.FontSize(8f));
                        text.Span("PLEASE NOTE: ").Bold().Underline();
                        text.Span("Write MB CODE and BATCH NO when you order.").Bold();
                    });
            });
        }

        private static LogoType ParseLogoType(string? logoTypeText)
        {
            if (string.IsNullOrWhiteSpace(logoTypeText))
                return LogoType.Vietaus;

            return Enum.TryParse<LogoType>(logoTypeText, true, out var result)
                ? result
                : LogoType.Vietaus;
        }

        private static (string imagePath, string companyName, string website) GetLogoInfo(LogoType logoType)
        {
            return logoType switch
            {
                LogoType.Vietaus => (Path.Combine(AppContext.BaseDirectory, "Assets", "Pdf", "VietAusLogo.png"), "VIETAUS POLYMER", "www.vietaus.com"),
                LogoType.LongGiang => (Path.Combine(AppContext.BaseDirectory, "Assets", "Pdf", "LongGiang.png"), "LONG GIANG", "-"),
                LogoType.AChau => (Path.Combine(AppContext.BaseDirectory, "Assets", "Pdf", "Achau.png"), "A CHAU", "-"),
                LogoType.Others => ("", "COMPANY NAME", "-"),
                _ => (Path.Combine(AppContext.BaseDirectory, "Assets", "Pdf", "VietAusLogo.png"), "VIETAUS POLYMER", "www.vietaus.com")
            };
        }

        private static string GetApprovalText(ColorChipRecordPdfModel model)
        {
            return ColorChipRecordPdfTextHelper.ResolveApprovalText(model);
        }

        private static string FormatDate(DateTime? dt)
        {
            return dt.HasValue
                ? dt.Value.ToString("dd MMM yy", CultureInfo.InvariantCulture)
                : string.Empty;
        }
    }
}
