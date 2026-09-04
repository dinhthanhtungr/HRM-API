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
    public class ColorChipRecordLandscapePdf : IColorChipRecordLandscapePdf
    {
        private const float titleFontSize = 16f;
        private const float contentFontSize = 8.5f;
        private const float labelFontSize = 7.5f;

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
                    page.Margin(10);
                    page.DefaultTextStyle(x => x.FontFamily("Times New Roman").FontSize(contentFontSize));

                    page.Content().Element(x => BuildContent(x, model, templateOnly));
                    page.Footer()
                        .PaddingBottom(20).Element(x => BuildFooter(x, model));
                });
            });

            return doc.GeneratePdf();
        }

        protected virtual void BuildContent(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Column(col =>
            {
                col.Spacing(4);

                col.Item().Element(x => BuildTopSection(x, m, templateOnly));

                col.Item()
                    .PaddingTop(2)
                    .AlignCenter()
                    .Text(string.IsNullOrWhiteSpace(m.Title) ? "COLOR CHIPS" : m.Title)
                    .FontSize(titleFontSize)
                    .Bold();
            });
        }
        protected virtual void BuildFooter(IContainer c, ColorChipRecordPdfModel m)
        {
            var isBatchNoEmpty = string.IsNullOrWhiteSpace(m.BatchNo);

            var leftText = isBatchNoEmpty
                ? "OPTION 1"
                : string.IsNullOrWhiteSpace(m.LowerText) ? "LOWER" : m.LowerText;

            var centerText = isBatchNoEmpty
                ? "OPTION 2"
                : string.IsNullOrWhiteSpace(m.StandardText) ? "STANDARD" : m.StandardText;

            var rightText = isBatchNoEmpty
                ? "OPTION 3"
                : string.IsNullOrWhiteSpace(m.UpperText) ? "UPPER" : m.UpperText;

            c.PaddingTop(2)
             .PaddingBottom(2)
             .Row(row =>
             {
                 row.RelativeItem()
                    .AlignCenter()
                    .Text(leftText)
                    .FontSize(contentFontSize)
                    .Bold();

                 row.RelativeItem()
                    .AlignCenter()
                    .Text(centerText)
                    .FontSize(contentFontSize)
                    .Bold();

                 row.RelativeItem()
                    .AlignCenter()
                    .Text(rightText)
                    .FontSize(contentFontSize)
                    .Bold();
             });
        }

        private void BuildTopSection(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Row(row =>
            {
                row.Spacing(8);

                row.RelativeItem(0.85f)
                    .AlignTop()
                    .Element(x => BuildLeftLogoSection(x, m));

                row.RelativeItem(2.55f)
                    .AlignTop()
                    .Element(x => BuildCenterInfoSection(x, m, templateOnly));

                row.RelativeItem(1.35f)
                    .AlignTop()
                    .Element(x => BuildApprovalSection(x, m, templateOnly));
            });
        }

        private void BuildLeftLogoSection(IContainer c, ColorChipRecordPdfModel m)
        {
            var logoType = ParseLogoType(m.LogoTypeText);
            var (imagePath, companyName, website) = GetLogoInfo(logoType);

            c.PaddingTop(6)
             .AlignTop()
             .AlignCenter()
             .Column(col =>
             {
                 col.Spacing(2);

                 col.Item().AlignCenter().Element(e =>
                 {
                     if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                         e.Height(58).Image(Image.FromFile(imagePath)).FitHeight();
                     else
                         e.Text("Logo").FontSize(contentFontSize);
                 });

                 col.Item()
                     .AlignCenter()
                     .Text(companyName)
                     .FontSize(9)
                     .Bold();

                 col.Item()
                     .AlignCenter()
                     .Text(website)
                     .FontSize(7);
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
                    "www.longgiang.com"
                ),

                LogoType.AChau => (
                    Path.Combine(AppContext.BaseDirectory, "Assets", "Pdf", "Achau.png"),
                    "A CHAU",
                    "www.achau.com"
                ),

                LogoType.Others => (
                    "",
                    "COMPANY NAME",
                    "www.company.com"
                ),

                _ => (
                    Path.Combine(AppContext.BaseDirectory, "Assets", "Pdf", "VietAusLogo.png"),
                    "VIETAUS POLYMER",
                    "www.vietaus.com"
                )
            };
        }

        private void BuildCenterInfoSection(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Column(col =>
            {
                AddInfoRowUnderline(col, "BATCH NO", templateOnly ? "SAMPLE" : m.BatchNo);
                AddInfoRowUnderline(col, "DATE", templateOnly ? "" : FormatDate(DateTime.Now));
                AddInfoRowUnderline(col, "CUSTOMER", templateOnly ? "" : m.Customer);
                AddInfoRowUnderline(col, "CODE", templateOnly ? "" : m.Code);
                AddInfoRowUnderline(col, "COLOR", templateOnly ? "" : m.Color);
                AddInfoRowUnderline(col, "ADD RATE", templateOnly ? "" : m.AddRate);
                AddInfoRowUnderline(col, "RESIN", templateOnly ? "" : m.Resin);
            });
        }

        private void AddInfoRowUnderline(ColumnDescriptor col, string label, string? value)
        {
            col.Item()
                .BorderBottom(0.5f)
                .BorderColor(Colors.Black)
                .PaddingVertical(3)
                .PaddingHorizontal(6)
                .Row(row =>
                {
                    row.ConstantItem(95)
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
             .Column(col =>
             {
                 col.Item()
                    .Background(Colors.Grey.Lighten2)
                    .BorderBottom(1)
                    .BorderColor(Colors.Black)
                    .PaddingVertical(4)
                    .PaddingHorizontal(6)
                    .AlignCenter()
                    .Text("APPROVAL")
                    .FontSize(contentFontSize)
                    .Bold();

                 col.Item()
                    .BorderBottom(1)
                    .BorderColor(Colors.Black)
                    .MinHeight(28)
                    .Padding(5)
                    .Text(templateOnly ? "" : ColorChipRecordPdfTextHelper.ResolveApprovalText(m))
                    .FontSize(labelFontSize);

                 col.Item()
                    .BorderBottom(1)
                    .BorderColor(Colors.Black)
                    .MinHeight(35.5f)
                    .Padding(5)
                    .Text(m.PrintNote);

                 col.Item().Element(x => BuildApprovalFooter(x, m, templateOnly));
             });
        }

        private void BuildApprovalFooter(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Column(col =>
            {
                col.Item()
                    .BorderBottom(1)
                    .BorderColor(Colors.Black)
                    .PaddingVertical(3)
                    .PaddingHorizontal(6)
                    .Text($"Prepared by: {(templateOnly ? "" : m.PreparedBy)}")
                    .FontSize(labelFontSize)
                    .Bold();

                col.Item().Row(row =>
                {
                    row.RelativeItem(1f)
                        .BorderRight(1)
                        .BorderColor(Colors.Black)
                        .PaddingVertical(3)
                        .PaddingHorizontal(6)
                        .Text("Signature:")
                        .FontSize(labelFontSize)
                        .Bold();

                    row.RelativeItem(0.7f)
                        .PaddingVertical(3)
                        .PaddingHorizontal(6)
                        .Text(templateOnly ? "" : m.Signature)
                        .FontSize(labelFontSize);
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
