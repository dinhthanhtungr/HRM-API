using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Globalization;
using System.IO;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Infrastructure.Documents.Pdfs.ColorChipRecords;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords
{
    public class ColorChipRecordTanPhuPdf : IColorChipRecordTanPhuPdf
    {
        private const float titleFontSize = 18f;
        private const float contentFontSize = 10f;
        private const float labelFontSize = 8.3f;
        private const float borderWidth = 1f;
        private readonly string? _sizeStandardOverride;
        private readonly string? _weightStandardOverride;

        public ColorChipRecordTanPhuPdf()
        {
        }

        internal ColorChipRecordTanPhuPdf(string sizeStandardOverride, string weightStandardOverride)
        {
            _sizeStandardOverride = sizeStandardOverride;
            _weightStandardOverride = weightStandardOverride;
        }

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

        protected virtual void BuildContent(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            c.Column(col =>
            {
                col.Spacing(6);

                col.Item()
                    .Height(95)
                    .Element(x => BuildTopLogoSection(x, m));

                col.Item()
                    .PaddingTop(4)
                    .Element(x => BuildInfoAndApprovalSection(x, m, templateOnly));

                col.Item()
                    .PaddingTop(2)
                    .AlignCenter()
                    .Text(string.IsNullOrWhiteSpace(m.Title) ? "COLOR CHIP" : m.Title)
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

        private void BuildInfoAndApprovalSection(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            var resinType = ParseResinType(m.ResinTypeText);
            var standard = ResinStandardSpecHelper.GetByResinType(resinType);

            var batchNo = templateOnly ? "SAMPLE" : m.BatchNo;
            var dateText = templateOnly ? "" : FormatDate(DateTime.Now);
            var customerText = templateOnly ? "" : m.Customer;
            var codeText = templateOnly ? "" : m.Code;
            var colorText = templateOnly ? "" : m.Color;
            var addRateText = templateOnly ? "" : m.AddRate;
            var resinText = templateOnly ? "" : m.Resin;
            var tempText = templateOnly ? "" : m.TemperatureLimit;
            var logoType = ParseLogoType(m.LogoTypeText);

            var sizeActualText = templateOnly
                ? ""
                : !string.IsNullOrWhiteSpace(m.SizeText) ? m.SizeText : "";

            var sizeStandardText = templateOnly
                ? ""
                : GetSizeStandardText(standard);

            var weightActualText = templateOnly
                ? ""
                : m.PelletWeightGram.HasValue && m.PelletWeightGram.Value > 0
                    ? m.PelletWeightGram.Value.ToString("0.##")
                    : "";

            var weightStandardText = templateOnly
                ? ""
                : GetWeightStandardText(standard);

            var electrostaticActualText = templateOnly
                ? ""
                : m.Electrostatic ?? false ? "Có" : "Không có";

            var electrostaticStandardText = templateOnly
                ? ""
                : standard.AntiStaticType == AntiStaticType.None ? "Không có" : "Có";

            var approvalCompanyName = ColorChipRecordPdfTextHelper.GetApprovalCompanyName(logoType);

            var approvalText = $"PLEASE RETURN ONE/TWO SETS TO {approvalCompanyName} UPON APPROVAL";

            var preparedByText = templateOnly ? "" : m.PreparedBy;

            c.Border(borderWidth)
             .BorderColor(Colors.Black)
             .Table(table =>
             {
                 table.ColumnsDefinition(columns =>
                 {
                     columns.ConstantColumn(105); // nhãn trái
                     columns.ConstantColumn(210); // dữ liệu thực tế
                     columns.RelativeColumn();    // approval / chuẩn
                 });

                 // Row 1
                 AddCell(table, "BATCH NO", bold: true);
                 AddCell(table, batchNo, alignCenter: true, bold: true);
                 AddCell(table, "APPROVAL", alignCenter: true, bold: true);

                 // Row 2
                 AddCell(table, "DATE", bold: true);
                 AddCell(table, dateText, alignCenter: true, bold: true);
                 AddApprovalBlock(table, approvalText, rowSpan: 2);
                 //AddCell(table, m.PrintNote, alignCenter: true, bold: true);

                 // Row 3
                 AddCell(table, "CUSTOMER", bold: true, minHeight: 22f);
                 AddCell(table, customerText, alignCenter: true, bold: true, minHeight: 22f);


                 // Row 4
                 AddCell(table, "CODE", bold: true);
                 AddCell(table, codeText, alignCenter: true, bold: true);
                 AddNoteBlock(table, m.PrintNote, rowSpan: 2);

                 // Row 5
                 AddCell(table, "COLOR", bold: true, minHeight: 26f);
                 AddCell(table, colorText, alignCenter: true, bold: true, minHeight: 26f);

                 // Row 6
                 AddCell(table, "ADD RATE", bold: true);
                 AddCell(table, addRateText, alignCenter: true, bold: true);
                 AddCell(table, $"Prepared by: {preparedByText}", alignCenter: true, bold: true);

                 // Row 7
                 AddCell(table, "RESIN", bold: true);
                 AddCell(table, resinText, alignCenter: true, bold: true);
                 AddPreparedByBlock(table, preparedByText, rowSpan: 2);

                 // Row 8
                 AddCell(table, "TEMPERATURE(°C)", bold: true);
                 AddCell(table, tempText, alignCenter: true, bold: true);

                 // Row 9
                 AddCell(table, "KÍCH THƯỚC\n(Diameter x Length)\n(mm)", bold: true, minHeight: 32f);
                 AddCell(table, sizeActualText, alignCenter: true, bold: true, minHeight: 32f);
                 AddCell(table, sizeStandardText, alignCenter: true, bold: true, minHeight: 32f);

                 // Row 10
                 AddCell(table, "TRỌNG LƯỢNG\n(Hạt/gram)", bold: true, minHeight: 22f);
                 AddCell(table, weightActualText, alignCenter: true, bold: true, minHeight: 22f);
                 AddCell(table, weightStandardText, alignCenter: true, bold: true, minHeight: 22f);

                 // Row 11
                 AddCell(table, "TĨNH ĐIỆN", bold: true);
                 AddCell(table, electrostaticActualText, alignCenter: true, bold: true);
                 AddCell(table, electrostaticStandardText, alignCenter: true, bold: true);
             });
        }

        private void AddCell(
            TableDescriptor table,
            string? text,
            bool alignCenter = false,
            bool bold = false,
            float fontSize = 8.2f,
            float minHeight = 17f)
        {
            table.Cell()
                .Border(borderWidth)
                .BorderColor(Colors.Black)
                .MinHeight(minHeight)
                .PaddingVertical(2)
                .PaddingHorizontal(4)
                .Element(cell =>
                {
                    var container = cell.AlignMiddle();

                    if (alignCenter)
                        container = container.AlignCenter();
                    else
                        container = container.AlignLeft();

                    var txt = container.Text(string.IsNullOrWhiteSpace(text) ? "" : text)
                        .FontSize(fontSize);

                    if (bold)
                        txt.Bold();
                });
        }

        private void AddApprovalBlock(
            TableDescriptor table,
            string? text,
            uint rowSpan = 2)
        {
            table.Cell()
                .RowSpan(rowSpan)
                .Border(borderWidth)
                .BorderColor(Colors.Black)
                .MinHeight(50)
                .Padding(2)
                .Element(cell =>
                {
                    cell.AlignMiddle()
                        .AlignCenter()
                        .Text(string.IsNullOrWhiteSpace(text) ? "" : text)
                        .FontSize(labelFontSize);
                });
        }

        private void AddNoteBlock(
            TableDescriptor table,
            string? text,
            uint rowSpan = 2)
        {
            table.Cell()
                .RowSpan(rowSpan)
                .Border(borderWidth)
                .BorderColor(Colors.Black)
                .Padding(2)
                .Element(cell =>
                {
                    cell.AlignMiddle()
                        .AlignCenter()
                        .Text(string.IsNullOrWhiteSpace(text) ? "" : text)
                        .FontSize(labelFontSize);
                });
        }

        private void AddPreparedByBlock(
            TableDescriptor table,
            string? preparedByText,
            uint rowSpan = 2)
        {
            table.Cell()
                .RowSpan(rowSpan)
                .Border(borderWidth)
                .BorderColor(Colors.Black)
                .MinHeight(40)
                .PaddingVertical(4)
                .PaddingHorizontal(6)
                .Element(cell =>
                {
                    cell.AlignMiddle()
                        .Column(col =>
                        {
                            col.Spacing(4);

                            col.Item()
                                .Text($"Signature ")
                                .FontSize(8.5f)
                                .Bold();
                        });
                });
        }


        protected virtual void BuildBottomSection(IContainer c, ColorChipRecordPdfModel m, bool templateOnly)
        {
            var rightText = m.StandardText ?? string.Empty;

            c.Column(col =>
            {
                col.Spacing(6);

                col.Item().Row(row =>
                {
                    if (string.IsNullOrWhiteSpace(rightText))
                    {
                        row.RelativeItem()
                            .AlignCenter()
                            .Text(m.BatchNo)
                            .FontSize(16)
                            .Bold();
                    }
                    else
                    {
                        row.RelativeItem()
                            .AlignCenter()
                            .Text(m.BatchNo)
                            .FontSize(16)
                            .Bold();

                        row.RelativeItem()
                            .AlignCenter()
                            .Text(rightText)
                            .FontSize(16)
                            .Bold();
                    }
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

        private ResinType ParseResinType(string? resinTypeText)
        {
            if (string.IsNullOrWhiteSpace(resinTypeText))
                return ResinType.Other;

            return Enum.TryParse<ResinType>(resinTypeText, true, out var result)
                ? result
                : ResinType.Other;
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

        private static string FormatDate(DateTime? dt)
        {
            if (!dt.HasValue)
                return string.Empty;

            return dt.Value.ToString("dd MMM yy", CultureInfo.InvariantCulture);
        }

        protected virtual string GetSizeStandardText(ResinStandardSpec standard)
        {
            return _sizeStandardOverride ?? standard.SizeText;
        }

        protected virtual string GetWeightStandardText(ResinStandardSpec standard)
        {
            return _weightStandardOverride
                ?? $"{standard.PelletWeightMinGram:0.##}-{standard.PelletWeightMaxGram:0.##}";
        }
    }
}
