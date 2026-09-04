using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords
{
    public class ColorChipRecordPortraitPdf : IColorChipRecordPortraitPdf
    {
        private const float titleFontSize = 18f;
        private const float contentFontSize = 10f;
        private const float labelFontSize = 8.5f;
        private const float infoValueFontSize = 9.5f;

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
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontFamily("Times New Roman").FontSize(contentFontSize));

                    page.Content().Element(x => BuildContent(x, model, templateOnly));

                    page.Footer()
                        .PaddingTop(6)
                        .Element(x => BuildBottomSection(x, model, templateOnly));


                });
            });

            return doc.GeneratePdf();
        }

        private void BuildContent(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Column(col =>
            {
                col.Spacing(6);

                col.Item()
                    .Height(95)
                    .Element(x => BuildTopLogoSection(x, m));

                col.Item()
                    .Element(x => BuildInfoAndApprovalSection(x, m, templateOnly));

                col.Item()
                    .PaddingTop(4)
                    .AlignCenter()
                    .Text(string.IsNullOrWhiteSpace(m.Title) ? "COLOR CHIPS" : m.Title)
                    .FontSize(titleFontSize)
                    .Bold();
            });
        }

        private void BuildTopLogoSection(IContainer c, ColorChipRecordPdfModel model)
        {
            var logoType = ParseLogoType(model.LogoTypeText);
            var (imagePath, companyName, website) = GetLogoInfo(logoType);

            c.AlignCenter()
             .AlignMiddle()
             .Column(col =>
             {
                 col.Spacing(2);

                 col.Item().AlignCenter().Element(e =>
                 {
                     if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                         e.Height(62).Image(Image.FromFile(imagePath)).FitHeight();
                     else
                         e.Text("Logo").FontSize(contentFontSize);
                 });

                 col.Item()
                     .AlignCenter()
                     .Text(companyName)
                     .FontSize(10)
                     .Bold();

                 col.Item()
                     .AlignCenter()
                     .Text(website)
                     .FontSize(7.5f);
             });
        }

        private LogoType ParseLogoType(string? logoTypeText)
        {
            if (string.IsNullOrWhiteSpace(logoTypeText))
                return LogoType.Vietaus;

            return Enum.TryParse<LogoType>(logoTypeText, true, out var result)
                ? result
                : LogoType.Vietaus;
        }

        private (string imagePath, string companyName, string website) GetLogoInfo(LogoType logoType)
        {
            return logoType switch
            {
                LogoType.Vietaus => (
                    Path.Combine(AppContext.BaseDirectory, "Assets", "Pdf", "VietAusLogo.png"),
                    "VIETAUS POLYMER",
                    "www.vietaus.com"
                ),

                LogoType.LongGiang => (
                    Path.Combine(AppContext.BaseDirectory, "Assets", "Pdf", "LongGiang.png"),
                    "LONG GIANG",
                    "-"
                ),

                LogoType.AChau => (
                    Path.Combine(AppContext.BaseDirectory, "Assets", "Pdf", "Achau.png"),
                    "A CHAU",
                    "-"
                ),

                LogoType.Others => (
                    "",
                    "COMPANY NAME",
                    "-"
                ),

                _ => (
                    Path.Combine(AppContext.BaseDirectory, "Assets", "Pdf", "VietAusLogo.png"),
                    "VIETAUS POLYMER",
                    "www.vietaus.com"
                )
            };
        }

        private void BuildInfoAndApprovalSection(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Row(row =>
            {
                row.Spacing(8);

                row.RelativeItem(1.2f)
                    .AlignTop()
                    .Element(x => BuildInfoSection(x, m, templateOnly));

                row.RelativeItem(0.95f)
                    .AlignTop()
                    .Element(x => BuildApprovalSection(x, m, templateOnly));
            });
        }

        private void BuildInfoSection(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.MinHeight(170)
             .Column(col =>
             {
                 AddInfoRowUnderline(col, "BATCH NO", templateOnly ? "SAMPLE" : m.BatchNo);
                 AddInfoRowUnderline(col, "DATE", templateOnly ? "" : FormatDate(DateTime.Now));
                 AddInfoRowUnderline(col, "CUSTOMER", templateOnly ? "" : m.Customer);
                 AddInfoRowUnderline(col, "CODE", templateOnly ? "" : m.Code);
                 AddInfoRowUnderline(col, "COLOR", templateOnly ? "" : m.Color, minHeight: 24);
                 AddInfoRowUnderline(col, "ADD RATE", templateOnly ? "" : m.AddRate);
                 AddInfoRowUnderline(col, "RESIN", templateOnly ? "" : m.Resin);
                 AddInfoRowUnderline(col, "TEMPERATURE(°C)", templateOnly ? "" : m.TemperatureLimit);
                 AddInfoRowUnderline(col, "MACHINE", templateOnly ? "" : m.Machine);
                 AddInfoRowUnderline(col, "DELTA E", templateOnly ? "" : m.DeltaE, isLastRow: true);
             });
        }

        private void AddInfoRowUnderline(
            ColumnDescriptor col,
            string label,
            string? value,
            float minHeight = 19f,
            bool isLastRow = false)
        {
            var item = col.Item()
                .MinHeight(minHeight)
                .PaddingTop(1)
                .PaddingBottom(1)
                .PaddingHorizontal(3);

            if (!isLastRow)
            {
                item = item
                    .BorderBottom(0.5f)
                    .BorderColor(Colors.Black);
            }

            item.Row(row =>
            {
                row.ConstantItem(102)
                    .AlignMiddle()
                    .Text(label)
                    .FontSize(labelFontSize)
                    .Bold();

                row.RelativeItem()
                    .AlignMiddle()
                    .AlignCenter()
                    .Text(string.IsNullOrWhiteSpace(value) ? "" : value)
                    .FontSize(contentFontSize)
                    .Bold();
            });
        }

        private void BuildApprovalSection(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Border(1)
             .BorderColor(Colors.Black)
             .MinHeight(170)
             .Column(col =>
             {
                 col.Item()
                    .Background(Colors.Grey.Lighten2)
                    .BorderBottom(1)
                    .BorderColor(Colors.Black)
                    .PaddingVertical(3)
                    .PaddingHorizontal(6)
                    .AlignCenter()
                    .Text("APPROVAL")
                    .FontSize(contentFontSize)
                    .Bold();

                 col.Item()
                    .BorderBottom(1)
                    .BorderColor(Colors.Black)
                    .MinHeight(34)
                    .PaddingHorizontal(6)
                    .PaddingVertical(4)
                    .AlignCenter()
                    .AlignMiddle()
                    .Text(templateOnly ? "" : ColorChipRecordPdfTextHelper.ResolveApprovalText(m, lineBreak: true))
                    .FontSize(labelFontSize);

                 col.Item()
                    .BorderBottom(1)
                    .BorderColor(Colors.Black)
                    .MinHeight(62)
                    .Padding(4)
                    .Text(m.PrintNote);

                 col.Item()
                    .Element(x => BuildApprovalFooter(x, m, templateOnly));
             });
        }

        private void BuildApprovalFooter(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Column(col =>
            {
                col.Item()
                    .BorderBottom(1)
                    .BorderColor(Colors.Black)
                    .PaddingVertical(2)
                    .PaddingHorizontal(6)
                    .Text($"Prepared by: {(templateOnly ? "" : m.PreparedBy)}")
                    .FontSize(labelFontSize)
                    .Bold();

                col.Item().Row(row =>
                {
                    row.RelativeItem(1f)
                        .BorderRight(1)
                        .BorderColor(Colors.Black)
                        .PaddingVertical(2)
                        .PaddingHorizontal(6)
                        .Text("Signature:")
                        .FontSize(labelFontSize)
                        .Bold();
                });
            });
        }

        private void BuildBottomSection(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            var isBatchNoEmpty = string.IsNullOrWhiteSpace(m.BatchNo);

            var leftText = isBatchNoEmpty
                ? "OPTION 1"
                : templateOnly ? "" : m.BatchNo;

            var rightText = isBatchNoEmpty
                ? "OPTION 2"
                : string.IsNullOrWhiteSpace(m.StandardText) ? "STANDARD" : m.StandardText;

            var isNonStandard =
                string.Equals(
                    m.FormStyleText,
                    FormStyle.Chips2_NonStandard.ToString(),
                    StringComparison.OrdinalIgnoreCase);

            c.Column(col =>
            {
                col.Spacing(6);

                if (isNonStandard)
                {
                    col.Item()
                        .AlignCenter()
                        .Text(isBatchNoEmpty ? "BATCH NO" : leftText)
                        .FontSize(16)
                        .Bold();
                }
                else
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem()
                            .AlignCenter()
                            .Text(leftText)
                            .FontSize(16)
                            .Bold();

                        row.RelativeItem()
                            .AlignCenter()
                            .Text(rightText)
                            .FontSize(16)
                            .Bold();
                    });
                }

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
        private static string FormatDate(DateTime? dt)
        {
            if (!dt.HasValue)
                return string.Empty;

            return dt.Value.ToString("dd MMM yy", CultureInfo.InvariantCulture);
        }
    }
}
