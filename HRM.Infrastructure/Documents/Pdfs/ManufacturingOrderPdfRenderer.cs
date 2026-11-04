using HRM.Application.Abstractions.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HRM.Application.Features.PLM.Manufacturing.Dtos;




namespace HRM.Infrastructure.Documents.Pdfs
{
    public sealed partial class ManufacturingOrderPdfRenderer : IManufacturingOrderPdfRenderer
    {
        private static readonly Guid PIGMENT_ID = Guid.Parse("d5fdf825-cca1-445c-9cdb-0bae7bd0d75b");
        private static readonly Guid POLYMER_ID = Guid.Parse("04fbe69b-dbd1-45ee-9361-50031955681b");
        private static readonly Guid ADDITIVE_ID = Guid.Parse("dacb8f0f-0152-474b-b719-761f784d6c78");
        private static readonly Guid OTHER_ID = Guid.Parse("da05ebd0-5cb2-4b23-993a-e11407b6734a");

        public byte[] RenderTemplate()
        {
            return Render(new ManufacturingOrderPdfDocument(), true);
        }

        public byte[] Render(ManufacturingOrderPdfDocument data, bool templateOnly = false)
        {
            data ??= new ManufacturingOrderPdfDocument();

            var doc = Document.Create(c =>
            {
                c.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(10);
                    page.DefaultTextStyle(t => t.FontFamily("Open Sans").FontSize(9));

                    page.Content().Element(x => BuildContent(x, data, templateOnly));
                    page.Footer().Component(new ManufacturingOrderPdfFooter());
                });
            });

            return doc.GeneratePdf();
        }

        private void BuildContent(IContainer c, ManufacturingOrderPdfDocument d, bool templateOnly)
        {
            var s = templateOnly ? new ManufacturingOrderPdfHeader() : (d.Header ?? new ManufacturingOrderPdfHeader());

            c.Column(col =>
            {
                col.Item().AlignCenter().PaddingTop(5)
                    .Text("LỆNH SẢN XUẤT").FontSize(14).Bold();

                col.Item().PaddingTop(5).AlignRight().Row(row =>
                {
                    row.Spacing(25);
                    var batchNo = templateOnly ? "" : (d.BatchNo ?? "");
                    var isVU = batchNo.StartsWith("VU", StringComparison.OrdinalIgnoreCase);
                    var prefix = isVU ? "VU" : "MFG";

                    row.AutoItem().Text(tx =>
                    {
                        tx.Span($"{prefix} No: ").FontSize(9);
                        tx.Span($"{batchNo}").FontSize(11).Black();

                        tx.Span(" - Batch#: ").FontSize(9);
                        tx.Span(templateOnly ? "" : $"{d.Header?.FormulaExternalId ?? "-"}")
                          .FontSize(11).Black();
                    });

                    row.AutoItem().Text(tx =>
                    {
                        //tx.Span("Ngày in: ").FontSize(9);
                        //tx.Span($"{DateTime.Now:dd/MM/yyyy}")
                        //  .FontSize(9).Black();

                        tx.Span("Ngày kế hoạch tạo phiếu: ").FontSize(9);
                        tx.Span(templateOnly ? "....../....../...... ..:.." : $"{d.CreatedDate:dd/MM/yyyy HH:mm}")
                                                  .FontSize(11).Black();
                    });

                });

                col.Item().PaddingTop(5).PaddingBottom(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(180);
                        cols.RelativeColumn();
                    });

                    void AddRow(string label, string? value)
                    {
                        table.Cell().Element(InfoCellStyle).Text(label).FontSize(8).SemiBold();
                        table.Cell().Element(InfoCellStyle).Text(string.IsNullOrWhiteSpace(value) ? "-" : value).FontSize(9);
                    }

                    static IContainer InfoCellStyle(IContainer container) =>
                        container.BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .PaddingVertical(3);

                    // ====== GRID 3 cột giống ảnh (FontSize 8) ======
                    // ====== GRID 3 cột (FontSize 8) + value đậm + thêm Khối lượng/mẻ ======
                    table.Cell().ColumnSpan(2).Element(x =>
                    {
                        x.Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2.2f); // trái
                                cols.RelativeColumn(1.6f); // giữa
                                cols.RelativeColumn(1.2f); // phải
                            });

                            // Tính khối lượng/mẻ
                            decimal? kgPerBatch = null;
                            if (s.NumOfBatches.HasValue && s.NumOfBatches.Value > 0 && s.TotalProductionQuantity.HasValue)
                                kgPerBatch = s.TotalProductionQuantity.Value / s.NumOfBatches.Value;

                            // gợi ý size
                            const float labelSize = 8f;
                            const float valueSize = 10f;   // tăng lên 1 chút cho nổi

                            // Row 1
                            t.Cell().Element(GridCell).Text(tx =>
                            {
                                tx.Span("Khách hàng: ").FontSize(labelSize);

                                tx.Span(templateOnly
                                    ? ""
                                    : $"{s.CustomerCode ?? ""} | {s.MerchandiseOrderExternalId ?? ""} | {s.PONo ?? ""}")
                                  .FontSize(valueSize).Black();
                            });


                            t.Cell().Element(GridCell).Text(tx =>
                            {
                                tx.Span("Colour Code: ").FontSize(labelSize);
                                tx.Span(templateOnly ? "" : $"{s.ColourCode ?? ""}")
                                  .FontSize(valueSize).Black();
                            });


                            t.Cell().Element(GridCell).Text(tx =>
                            {
                                tx.Span("Khối lượng/mẻ (Kg): ").FontSize(labelSize);
                                tx.Span(templateOnly ? "" : (kgPerBatch.HasValue ? $"{FormatKgPretty(kgPerBatch, 2)}" : "-"))
                                  .FontSize(valueSize).Black();
                            });


                            // Row 2
                            t.Cell().Element(GridCell).Text(tx =>
                            {
                                tx.Span("Tên sp: ").FontSize(labelSize);
                                tx.Span(templateOnly ? "" : $"{s.Name ?? ""}")
                                  .FontSize(valueSize).Black();
                            });

                            t.Cell().Element(GridCell).Text(tx =>
                            {
                                tx.Span("Kiểm tra: ").FontSize(labelSize);
                                tx.Span(templateOnly ? "" : $"{s.QcCheck ?? ""}")
                                  .FontSize(valueSize).Black();
                            });



                            t.Cell().Element(GridCell).Text(tx =>
                            {
                                tx.Span("Số mẻ: ").FontSize(labelSize);
                                tx.Span(templateOnly ? "" : $"{s.NumOfBatches?.ToString() ?? ""}")
                                  .FontSize(valueSize).Black();
                            });

                            // Row 3 (mới): 1 ô nội dung + 2 ô trống
                            t.Cell().Element(GridCell).Text(tx =>
                            {
                                tx.Span("Ngày yêu cầu: ").FontSize(labelSize);
                                tx.Span(templateOnly ? "....../....../......" : $"{d.RequestDate:dd/MM/yyyy}")
                                  .FontSize(valueSize).Black();
                            });
                            t.Cell().Element(GridCell).Text(tx =>
                            {
                                tx.Span("Tỷ lệ sử dụng: ").FontSize(labelSize);
                                tx.Span(templateOnly ? "" : $"{s.UsageRate?.ToString() ?? ""} %")
                                  .FontSize(valueSize).Black();
                            });
                            t.Cell().Element(GridCell).Text(tx =>
                            {
                                tx.Span("Tổng khối lượng (kg): ").FontSize(labelSize);
                                tx.Span(templateOnly ? "" : $"{FormatKgPretty(s.TotalProductionQuantity, 2)}")
                                  .FontSize(valueSize).Black();
                            });

                        });

                        static IContainer GridCell(IContainer c) =>
                            c.Border(1).BorderColor(Colors.Black)
                             .PaddingVertical(2).PaddingHorizontal(4)
                             .AlignMiddle();
                    });


                    AddRow(
                        "Yêu cầu ca SX và QC thực hiện:",
                        templateOnly ? "" : $"{d.BagType} :{s.LabNote}"
                    );

                    var req = templateOnly ? "" : s.Requirement;
                    var note = templateOnly ? "" : s.PlpuNote;

                    table.Cell().ColumnSpan(2)
                        .PaddingTop(6)
                        .Element(x => BuildNoteRequirementBox(x, note, req));

                });

                col.Item().PaddingTop(10).Element(x => BuildMaterialsTable(x, d, templateOnly));

                col.Item().PaddingTop(6).Element(x => BuildFormulaHistorySection(x, d, templateOnly));

                col.Item().PaddingTop(10).Element(x => BuildMachineSetupSection(x, d, templateOnly));


            });
        }

        private static void BuildNoteRequirementBox(IContainer c, string? note, string? requirement)
        {
            c.Table(t =>
            {
                t.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);  // Note
                    cols.RelativeColumn(1);  // Requirement
                });

                // Header row (xám)
                t.Cell().Element(BoxHeaderCell).Text("Note/Lưu ý").SemiBold();
                t.Cell().Element(BoxHeaderCell).Text("Phản hồi trước của khách hàng").SemiBold();

                // Content row
                t.Cell().Element(BoxBodyCell).Text(string.IsNullOrWhiteSpace(note) ? "" : note).FontSize(9);
                t.Cell().Element(BoxBodyCell).Text(string.IsNullOrWhiteSpace(requirement) ? "" : requirement).FontSize(9);
            });

            static IContainer BoxHeaderCell(IContainer x) =>
                x.Border(1).BorderColor(Colors.Black)
                 .Background(Colors.Grey.Lighten2)
                 .PaddingVertical(4).PaddingHorizontal(6);

            static IContainer BoxBodyCell(IContainer x) =>
                x.Border(1).BorderColor(Colors.Black)
                 .MinHeight(35)              // tăng/giảm tuỳ bạn, giống khung ảnh
                 .PaddingVertical(4).PaddingHorizontal(6)
                 .AlignTop();
        }

        private void BuildFormulaHistorySection(IContainer c, ManufacturingOrderPdfDocument d, bool templateOnly)
        {
            var formulaHistoryText = templateOnly
                ? ""
                : (d.FormulaSelectList ?? "");

            var copiedFormulaText = string.IsNullOrWhiteSpace(d.CopiedFromCode)
                ? "-"
                : d.CopiedFromCode;


            c.Border(1)
             .BorderColor(Colors.Black)
             .Row(row =>
             {
                 row.ConstantItem(180).BorderRight(1).BorderColor(Colors.Black).Column(left =>
                 {
                     left.Item()
                         .Background(Colors.Grey.Lighten2)
                         .PaddingVertical(4)
                         .PaddingHorizontal(6)
                         .Text("Công thức copy từ")
                         .FontSize(9)
                         .SemiBold();

                     left.Item()
                         .PaddingVertical(4)
                         .PaddingHorizontal(6)
                         .MinHeight(24)
                         .AlignMiddle()
                         .Text(text =>
                         {
                             if (templateOnly || string.IsNullOrWhiteSpace(copiedFormulaText))
                             {
                                 text.Span("-").FontSize(9);
                                 return;
                             }

                             text.Span(copiedFormulaText).FontSize(9);
                         });
                 });

                 row.RelativeItem().Column(right =>
                 {
                     right.Item()
                         .Background(Colors.Grey.Lighten2)
                         .PaddingVertical(4)
                         .PaddingHorizontal(6)
                         .Text("Lịch sử công thức đã từng chạy")
                         .FontSize(9)
                         .SemiBold();

                     right.Item()
                         .PaddingVertical(4)
                         .PaddingHorizontal(6)
                         .MinHeight(24)
                         .AlignMiddle()
                         .Text(text =>
                         {
                             if (templateOnly || string.IsNullOrWhiteSpace(formulaHistoryText))
                             {
                                 text.Span("-").FontSize(9);
                                 return;
                             }

                             text.Span(formulaHistoryText).FontSize(9);
                         });
                 });
             });



        }

    }
}
