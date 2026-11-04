using System.Globalization;
using HRM.Application.Features.PLM.Manufacturing.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs;

public sealed partial class ManufacturingOrderPdfRenderer
{
        private void BuildMaterialsTable(IContainer c, ManufacturingOrderPdfDocument d, bool templateOnly)
        {
            var items = templateOnly
                ? new List<ManufacturingOrderPdfMaterial>()
                : (d.Materials ?? new List<ManufacturingOrderPdfMaterial>())
                    .Where(x => x != null && x.Quantity > 0)
                    .ToList();

            var s = templateOnly
                ? new ManufacturingOrderPdfHeader()
                : (d.Header ?? new ManufacturingOrderPdfHeader());

            var batchQty = s.TotalProductionQuantity ?? 0m;
            var batches = s.NumOfBatches ?? 1;
            if (batches < 1) batches = 1;

            c.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1.50f); // Code
                    cols.RelativeColumn(3.40f); // Name
                    cols.RelativeColumn(1.10f); // Lot #
                    cols.RelativeColumn(1.20f); // Std (kg)
                    cols.RelativeColumn(1.35f); // SL / mẻ (kg)
                    cols.RelativeColumn(1.25f); // SL / mẻ (g)
                    cols.RelativeColumn(1.45f); // SL tổng (kg)
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text("Code").FontSize(9).Bold();
                    h.Cell().Element(HeaderCell).Text("Name").FontSize(9).Bold();
                    h.Cell().Element(HeaderCell).Text("Lot #").FontSize(9).Bold();
                    h.Cell().Element(HeaderCell).AlignMiddle().Text("Std").FontSize(9).Bold();
                    h.Cell().Element(HeaderCell).AlignMiddle().Text("SL / mẻ (kg)").FontSize(9).Bold();
                    h.Cell().Element(HeaderCell).AlignMiddle().Text("SL / mẻ (g)").FontSize(9).Bold();
                    h.Cell().Element(HeaderCell).AlignMiddle().Text("SL tổng (kg)").FontSize(9).Bold();
                });

                if (templateOnly)
                {
                    table.Cell().ColumnSpan(7).Element(GroupTitleCell)
                        .AlignCenter().Text("Nguyên vật liệu / Materials").SemiBold();

                    for (int i = 0; i < 10; i++)
                    {
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("");
                    }

                    table.Cell().ColumnSpan(3).Element(TotalLabelCell)
                        .AlignCenter().Text("Tổng").SemiBold();

                    table.Cell().Element(TotalCell).Text("");
                    table.Cell().Element(TotalCell).Text("");
                    table.Cell().Element(TotalCell).Text("");
                    table.Cell().Element(TotalCell).Text("");
                }
                else
                {
                    var groups = items
                        .GroupBy(x => x.CategoryId)
                        .OrderBy(g => CategorySortKey(g.Key))
                        .ToList();

                    decimal sumStd = 0m;
                    decimal sumPerBatch = 0m;
                    decimal sumTotal = 0m;

                    foreach (var g in groups)
                    {
                        table.Cell().ColumnSpan(7).Element(GroupTitleCell)
                            .AlignCenter().Text(CategoryTitle(g.Key)).SemiBold();

                        foreach (var m in g.OrderBy(x => x.ExternalId))
                        {
                            var std = m.Quantity;
                            var perBatchKg = std * (batchQty / batches);
                            var totalKg = perBatchKg * batches;

                            sumStd += std;
                            sumPerBatch += perBatchKg;
                            sumTotal += totalKg;

                            table.Cell().Element(BodyCell).Text(m.ExternalId).FontSize(10).Bold();
                            table.Cell().Element(BodyCell).Text(m.MaterialName).FontSize(10).ClampLines(2).Bold();
                            table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(m.LotNo) ? "" : m.LotNo).FontSize(8).Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatKgPretty(std, 7)).FontSize(10).Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatKgPretty(perBatchKg, 5)).FontSize(10).Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatGPretty(perBatchKg, 2)).FontSize(10).Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatKgPretty(totalKg, 5)).FontSize(10).Bold();
                        }
                    }

                    table.Cell().ColumnSpan(3).Element(TotalLabelCell)
                        .AlignCenter().Text("Tổng").SemiBold();

                    table.Cell().Element(TotalCell).AlignRight().Text(FormatKgPretty(sumStd, 6)).FontSize(10).Bold();
                    table.Cell().Element(TotalCell).AlignRight().Text(FormatKgPretty(sumPerBatch, 6)).FontSize(10).Bold();
                    table.Cell().Element(TotalCell).AlignRight().Text(FormatGPretty(sumPerBatch, 2)).FontSize(10).Bold();
                    table.Cell().Element(TotalCell).AlignRight().Text(FormatKgPretty(sumTotal, 6)).FontSize(10).Bold();
                }
            });

            static IContainer HeaderCell(IContainer x) =>
                x.Border(0.5f).BorderColor(Colors.Black)
                    .Background(Colors.Grey.Lighten2)
                    .PaddingVertical(2).PaddingHorizontal(2);

            static IContainer GroupTitleCell(IContainer x) =>
                x.BorderLeft(0.5f).BorderRight(0.5f).BorderBottom(0.5f).BorderTop(0.5f)
                    .BorderColor(Colors.Black)
                    .PaddingVertical(2).PaddingHorizontal(2);

            static IContainer BodyCell(IContainer x) =>
                x.BorderLeft(0.5f).BorderRight(0.5f).BorderBottom(0.5f)
                 .BorderColor(Colors.Black)
                 .PaddingVertical(2).PaddingHorizontal(4);

            static IContainer TotalCell(IContainer x) =>
                x.BorderLeft(0.5f).BorderRight(0.5f).BorderBottom(0.5f)
                 .BorderColor(Colors.Black)
                 .PaddingVertical(2).PaddingHorizontal(4);

            static IContainer TotalLabelCell(IContainer x) =>
                x.BorderLeft(0.5f).BorderRight(0.5f).BorderBottom(0.5f)
                    .BorderColor(Colors.Black)
                    .PaddingVertical(2).PaddingHorizontal(2);
        }
        private static readonly CultureInfo _inv = CultureInfo.InvariantCulture;

        // Không scientific notation (E), giữ tối đa N chữ số thập phân (mặc định 50)
        private static string NoSci(decimal v, int decimals = 50)
        {
            // 0.########.... (decimals lần) => không E, không trailing zero thừa
            var fmt = "0." + new string('#', decimals);
            return v.ToString(fmt, _inv);
        }

        private static string NoSci(decimal? v, int decimals = 50)
            => v.HasValue ? NoSci(v.Value, decimals) : "-";

        // FormatKg = wrapper để bạn không cần sửa các chỗ gọi Text(FormatKg(...))
        private static string FormatKg(decimal v) => FormatKgPretty(v, 6);
        private static string FormatKg(decimal? v) => FormatKgPretty(v, 2);

        private static string FormatG(decimal v) => FormatGPretty(v, 0);
        private static string FormatG(decimal? v) => FormatGPretty(v, 0);

        private static string Fmt(decimal v, int decimals)
        {
            // 1,234.567 (dấu , hàng nghìn; dấu . thập phân)
            var fmt = "#,##0" + (decimals > 0 ? "." + new string('0', decimals) : "");
            return v.ToString(fmt, CultureInfo.InvariantCulture);
        }

        private static string Fmt(decimal? v, int decimals) => v.HasValue ? Fmt(v.Value, decimals) : "-";

        private static string FormatKgPretty(decimal v, int decimals = 3) => Fmt(v, decimals);
        private static string FormatKgPretty(decimal? v, int decimals = 3) => Fmt(v, decimals);

        private static string FormatGPretty(decimal v, int decimals = 0) => Fmt(v * 1000m, decimals);
        private static string FormatGPretty(decimal? v, int decimals = 0) => v.HasValue ? FormatGPretty(v.Value, decimals) : "-";
        private int CategorySortKey(Guid categoryId)
        {
            if (categoryId == PIGMENT_ID) return 1;
            if (categoryId == ADDITIVE_ID) return 2;
            if (categoryId == POLYMER_ID) return 3;
            if (categoryId == OTHER_ID) return 4;
            return 99;
        }

        private string CategoryTitle(Guid categoryId)
        {
            if (categoryId == PIGMENT_ID) return "Pigments/Bột màu";
            if (categoryId == ADDITIVE_ID) return "Additives/Phụ gia";
            if (categoryId == POLYMER_ID) return "Plastic/Nhựa";
            return "Others/Khác";
        }

}
