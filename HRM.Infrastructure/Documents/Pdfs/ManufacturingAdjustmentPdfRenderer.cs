using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;

using HRM.Application.Abstractions.Documents;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Infrastructure.Documents.Pdfs
{
    internal sealed class ManufacturingAdjustmentPdfRenderer : IManufacturingAdjustmentPdfRenderer
    {
        private static readonly Guid PigmentId = Guid.Parse("d5fdf825-cca1-445c-9cdb-0bae7bd0d75b");
        private static readonly Guid PolymerId = Guid.Parse("04fbe69b-dbd1-45ee-9361-50031955681b");
        private static readonly Guid AdditiveId = Guid.Parse("dacb8f0f-0152-474b-b719-761f784d6c78");
        private const int TargetBodyRowsPerPage = 20;

        public byte[] Render(PdfMfgFormulaAdjustment adjustment)
        {
            adjustment ??= new PdfMfgFormulaAdjustment();
            var groups = adjustment.Batches.Count == 0
                ? new List<PdfBatchGroup> { new(new PdfMfgFormulaAdjustmentBatch(), 0, 0, string.Empty) }
                : GroupBatchesBySignature(adjustment.Batches);

            var doc = Document.Create(c =>
            {
                foreach (var group in groups)
                {
                    c.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(28);
                        page.DefaultTextStyle(t => t.FontFamily("Open Sans").FontSize(9));
                        page.Content().Element(x => BuildPage(x, adjustment, group.BatchTemplate, group.DisplayRange));
                        page.Footer()
                            .PaddingTop(4)
                            .AlignRight()
                            .Text(text =>
                            {
                                text.Span("Trang ");
                                text.CurrentPageNumber();
                            });
                    });
                }
            });

            return doc.GeneratePdf();
        }

        private static void BuildPage(IContainer container, PdfMfgFormulaAdjustment adjustment, PdfMfgFormulaAdjustmentBatch batch, string batchRange)
        {
            container.Border(1).BorderColor(Colors.Black).Layers(layers =>
            {
                layers.PrimaryLayer().PaddingBottom(24).Column(col =>
                {
                    col.Item().Height(26).BorderBottom(1).AlignCenter().AlignMiddle()
                        .Text("PHIẾU CHỈNH MÀU").FontSize(15).Bold();

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(95);
                            cols.RelativeColumn();
                            cols.ConstantColumn(120);
                            cols.RelativeColumn();
                        });

                        Cell(table.Cell()).Text("Ngày tạo phiếu:").SemiBold();
                        Cell(table.Cell()).Text(FormatDate(batch.CreatedDate == default ? adjustment.CreatedDate : batch.CreatedDate));
                        Cell(table.Cell()).Text("Khối lượng sau chỉnh tổng (g):").SemiBold();
                        Cell(table.Cell()).Text(FormatQuantity(GetDisplayFinalQuantity(batch))).FontSize(10);

                        Cell(table.Cell()).Text("Mã hàng:").SemiBold();
                        table.Cell().ColumnSpan(3).Element(Cell).Text(BuildProductText(adjustment));

                        Cell(table.Cell()).Text("Số lần chỉnh màu").SemiBold();
                        Cell(table.Cell()).Text(batch.TrialNo > 0 ? batch.TrialNo.ToString() : string.Empty);
                        Cell(table.Cell()).Text("Mẻ:").SemiBold();
                        Cell(table.Cell()).Text(batchRange);

                        Cell(table.Cell()).Text("LSX:").SemiBold();
                        Cell(table.Cell()).Text(adjustment.ProductionOrderCode ?? string.Empty);
                        Cell(table.Cell()).Text("Công thức:").SemiBold();
                        Cell(table.Cell()).Text(adjustment.FormulaCode ?? string.Empty);
                    });

                    col.Item().Element(x => BuildBatchNoteRows(x, adjustment, batch));
                    col.Item().Element(x => BuildAdjustmentTable(x, batch));
                });

                layers.Layer().AlignBottom().Element(x => BuildBottomRows(x, adjustment, batch));
            });
        }

        private static void BuildBatchNoteRows(IContainer container, PdfMfgFormulaAdjustment adjustment, PdfMfgFormulaAdjustmentBatch batch)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(95);
                    cols.RelativeColumn();
                });

                Cell(table.Cell()).Text("Ghi chú mẻ").SemiBold();
                Cell(table.Cell()).Text(batch.Note ?? adjustment.Note ?? string.Empty);
            });
        }

        private static void BuildAdjustmentTable(IContainer container, PdfMfgFormulaAdjustmentBatch batch)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(70);
                    cols.RelativeColumn(1.1f);
                    cols.RelativeColumn(2.0f);
                    cols.RelativeColumn(1.0f);
                    cols.RelativeColumn(1.2f);
                });

                Header(table.Cell()).Text("Nhóm").Bold();
                Header(table.Cell()).Text("CODE").Bold();
                Header(table.Cell()).Text("Tên").Bold();
                Header(table.Cell()).Text(GetItemQuantityHeader(batch)).Bold();
                Header(table.Cell()).Text("Lot / Ghi chú").Bold();

                var printableItems = batch.Items
                    .Where(item => ShouldPrintItem(batch, item))
                    .ToList();

                var pigmentItems = printableItems.Where(x => x.CategoryId == PigmentId).OrderBy(x => x.LineNo).ToList();
                var additiveItems = printableItems.Where(x => x.CategoryId == AdditiveId).OrderBy(x => x.LineNo).ToList();
                var polymerItems = printableItems.Where(x => x.CategoryId == PolymerId).OrderBy(x => x.LineNo).ToList();
                var otherItems = printableItems
                    .Where(x => x.CategoryId != PigmentId && x.CategoryId != AdditiveId && x.CategoryId != PolymerId)
                    .OrderBy(x => x.LineNo)
                    .ToList();
                var minRows = BuildSectionMinRows(pigmentItems.Count, additiveItems.Count, polymerItems.Count, otherItems.Count);

                AddSection(table, batch, "BỘT MÀU", pigmentItems, minRows.PigmentRows);
                AddSection(table, batch, "PHỤ GIA", additiveItems, minRows.AdditiveRows);
                AddSection(table, batch, "NHỰA", polymerItems, minRows.PolymerRows);
                AddSection(table, batch, "KHÁC", otherItems, minRows.OtherRows);
            });
        }

        private static void AddSection(
            TableDescriptor table,
            PdfMfgFormulaAdjustmentBatch batch,
            string title,
            IEnumerable<PdfMfgFormulaAdjustmentItem> source,
            int minRows)
        {
            var items = source.OrderBy(x => x.LineNo).ToList();
            var rowCount = Math.Max(minRows, items.Count);

            for (var i = 0; i < rowCount; i++)
            {
                var item = i < items.Count ? items[i] : null;
                Body(table.Cell()).Text(i == 0 ? title : string.Empty).Bold();
                Body(table.Cell()).Text(item?.Code ?? string.Empty).FontSize(10).Bold();
                Body(table.Cell()).Text(item?.Name ?? string.Empty);
                Body(table.Cell()).AlignRight().Text(FormatQuantity(GetDisplayItemQuantity(batch, item))).FontSize(11).Bold();
                Body(table.Cell()).Text(BuildLotNote(item));
            }
        }

        private static void BuildBottomRows(
            IContainer container,
            PdfMfgFormulaAdjustment adjustment,
            PdfMfgFormulaAdjustmentBatch batch)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(95);
                    cols.RelativeColumn();
                    cols.ConstantColumn(95);
                    cols.RelativeColumn();
                });

                Cell(table.Cell()).Text("NV QC").SemiBold();
                Cell(table.Cell()).Text(batch.CreatedByName ?? adjustment.CreatedByName ?? string.Empty);
                Cell(table.Cell()).Text("Ngày in phiếu").SemiBold();
                Cell(table.Cell()).Text(FormatDate(DateTime.Now));
            });
        }

        private static IContainer Header(IContainer container) =>
            container.Border(0.75f).BorderColor(Colors.Black).Background(Colors.Grey.Lighten2)
                .PaddingVertical(3).PaddingHorizontal(3).AlignMiddle().AlignCenter();

        private static IContainer Body(IContainer container) =>
            container.Border(0.75f).BorderColor(Colors.Black).MinHeight(22)
                .PaddingVertical(2).PaddingHorizontal(3).AlignMiddle();

        private static IContainer Cell(IContainer container) =>
            container.Border(0.75f).BorderColor(Colors.Black).MinHeight(21)
                .PaddingVertical(2).PaddingHorizontal(4).AlignMiddle();

        private static string BuildProductText(PdfMfgFormulaAdjustment adjustment)
        {
            if (string.IsNullOrWhiteSpace(adjustment.ProductName))
                return adjustment.ProductCode ?? string.Empty;

            return string.IsNullOrWhiteSpace(adjustment.ProductCode)
                ? adjustment.ProductName
                : $"{adjustment.ProductCode} - {adjustment.ProductName}";
        }

        private static string BuildLotNote(PdfMfgFormulaAdjustmentItem? item)
        {
            if (item == null)
                return string.Empty;

            if (string.IsNullOrWhiteSpace(item.LotNo))
                return item.Note ?? string.Empty;

            return string.IsNullOrWhiteSpace(item.Note)
                ? item.LotNo
                : $"{item.LotNo} - {item.Note}";
        }

        private static string FormatDate(DateTime value) =>
            value == default ? string.Empty : value.ToString("dd/MM/yyyy HH:mm");

        private static string FormatQuantity(decimal? value) =>
            value.HasValue ? value.Value.ToString("#,0.###") : string.Empty;

        private static decimal? GetDisplayFinalQuantity(PdfMfgFormulaAdjustmentBatch batch)
        {
            var adjustedTotal = batch.Items
                .Where(x => x.AdjustedQuantity.HasValue)
                .Sum(x => x.AdjustedQuantity!.Value);

            var deltaTotal = batch.Items
                .Where(x => x.DeltaQuantity.HasValue)
                .Sum(x => x.DeltaQuantity!.Value);

            if (batch.FinalQuantityGram.HasValue)
                return batch.FinalQuantityGram.Value;

            var baseQuantity = GetBaseQuantityGram(batch);
            var fallbackFinalQuantity = baseQuantity.HasValue
                ? baseQuantity.Value + deltaTotal
                : (decimal?)null;

            if (fallbackFinalQuantity.HasValue)
                return fallbackFinalQuantity;

            if (adjustedTotal > 0m)
                return adjustedTotal;

            return PositiveOrNull(deltaTotal);
        }

        private static decimal? PositiveOrNull(decimal value) =>
            value > 0m ? value : null;

        private static decimal? GetBaseQuantityGram(PdfMfgFormulaAdjustmentBatch batch)
        {
            var baseQuantity = batch.IsAdditionalBatch
                ? batch.BatchQuantity
                    ?? batch.TakenQuantityGram
                    ?? PositiveOrNull(batch.Items
                        .Where(x => x.BaseQuantity.HasValue)
                        .Sum(x => MfgAdjustmentQuantityCalculator.ToGram(x.BaseQuantity!.Value, x.Unit)))
                : batch.TakenQuantityGram
                    ?? batch.BatchQuantity
                    ?? PositiveOrNull(batch.Items
                        .Where(x => x.BaseQuantity.HasValue)
                        .Sum(x => MfgAdjustmentQuantityCalculator.ToGram(x.BaseQuantity!.Value, x.Unit)));

            if (!baseQuantity.HasValue)
                return null;

            return baseQuantity.Value;
        }

        private static decimal? GetDisplayItemQuantity(PdfMfgFormulaAdjustmentBatch batch, PdfMfgFormulaAdjustmentItem? item)
        {
            if (item == null)
                return null;

            if (batch.AdjustmentType == AdjustmentType.SetQuantity)
            {
                var quantity = item.AdjustedQuantity ?? item.DeltaQuantity;
                return quantity.HasValue
                    ? MfgAdjustmentQuantityCalculator.ToGram(quantity.Value, item.Unit)
                    : null;
            }

            if (item.DeltaQuantity.HasValue)
                return MfgAdjustmentQuantityCalculator.ToGram(item.DeltaQuantity.Value, item.Unit);

            if (item.BaseQuantity.HasValue && item.AdjustedQuantity.HasValue)
                return MfgAdjustmentQuantityCalculator.ToGram(
                    item.AdjustedQuantity.Value - item.BaseQuantity.Value,
                    item.Unit);

            return null;
        }

        private static bool ShouldPrintItem(
            PdfMfgFormulaAdjustmentBatch batch,
            PdfMfgFormulaAdjustmentItem item)
        {
            var displayQuantity = GetDisplayItemQuantity(batch, item);
            return displayQuantity.HasValue && displayQuantity.Value != 0m;
        }

        private static string GetItemQuantityHeader(PdfMfgFormulaAdjustmentBatch batch) =>
            batch.AdjustmentType == AdjustmentType.SetQuantity
                ? "Khối lượng cân mới (g)"
                : "Khối lượng thêm vào (g)";

        private static SectionMinRows BuildSectionMinRows(int pigmentCount, int additiveCount, int polymerCount, int otherCount)
        {
            var rows = new[]
            {
                Math.Max(6, pigmentCount),
                Math.Max(4, additiveCount),
                Math.Max(4, polymerCount),
                otherCount > 0 ? Math.Max(3, otherCount) : 0
            };

            var extraRows = Math.Max(0, TargetBodyRowsPerPage - rows.Sum());
            var fillIndexes = otherCount > 0
                ? new[] { 0, 1, 2, 3 }
                : new[] { 0, 1, 2 };

            for (var i = 0; i < extraRows; i++)
            {
                rows[fillIndexes[i % fillIndexes.Length]]++;
            }

            return new SectionMinRows(rows[0], rows[1], rows[2], rows[3]);
        }

        private readonly record struct SectionMinRows(int PigmentRows, int AdditiveRows, int PolymerRows, int OtherRows);

        private static List<PdfBatchGroup> GroupBatchesBySignature(List<PdfMfgFormulaAdjustmentBatch> batches)
        {
            var result = new List<PdfBatchGroup>();

            foreach (var batch in batches.OrderBy(x => x.BatchNo).ThenBy(x => x.TrialNo))
            {
                var signature = BuildBatchSignature(batch);
                var last = result.LastOrDefault();

                if (last is not null &&
                    last.Signature == signature &&
                    last.BatchTo + 1 == batch.BatchNo)
                {
                    last.BatchTo = batch.BatchNo;
                    continue;
                }

                result.Add(new PdfBatchGroup(batch, batch.BatchNo, batch.BatchNo, signature));
            }

            return result;
        }

        private static string BuildBatchSignature(PdfMfgFormulaAdjustmentBatch batch)
        {
            var items = batch.Items
                .OrderBy(x => x.LineNo)
                .ThenBy(x => x.Code)
                .Select(x => string.Join("|",
                    NormalizeGuid(x.CategoryId),
                    NormalizeText(x.Code),
                    NormalizeText(x.Name),
                    NormalizeDecimal(x.BaseQuantity),
                    NormalizeDecimal(x.AdjustedQuantity),
                    NormalizeDecimal(x.DeltaQuantity),
                    NormalizeText(x.Unit),
                    NormalizeText(x.LotNo),
                    NormalizeText(x.Note)
                ));

            return string.Join("::",
                batch.TrialNo,
                batch.AdjustmentType.ToString(),
                NormalizeDecimal(batch.BatchQuantity),
                NormalizeDecimal(batch.TakenQuantityGram),
                NormalizeDecimal(batch.FinalQuantityGram),
                NormalizeText(batch.CreatedByName),
                NormalizeText(batch.Note),
                string.Join(";;", items)
            );
        }

        private static string NormalizeGuid(Guid value)
        {
            return value != Guid.Empty
                ? value.ToString("N")
                : string.Empty;
        }

        private static string NormalizeDecimal(decimal? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.##########", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private sealed class PdfBatchGroup
        {
            public PdfBatchGroup(
                PdfMfgFormulaAdjustmentBatch batchTemplate,
                int batchFrom,
                int batchTo,
                string signature)
            {
                BatchTemplate = batchTemplate;
                BatchFrom = batchFrom;
                BatchTo = batchTo;
                Signature = signature;
            }

            public PdfMfgFormulaAdjustmentBatch BatchTemplate { get; }
            public int BatchFrom { get; }
            public int BatchTo { get; set; }
            public string Signature { get; }
            public string DisplayRange => BatchFrom <= 0
                ? string.Empty
                : BatchFrom == BatchTo ? BatchFrom.ToString() : $"{BatchFrom} ... {BatchTo}";
        }
    }
}
