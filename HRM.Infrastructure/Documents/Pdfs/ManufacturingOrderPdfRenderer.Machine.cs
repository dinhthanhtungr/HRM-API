using System.Globalization;
using HRM.Application.Features.PLM.Manufacturing.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs;

public sealed partial class ManufacturingOrderPdfRenderer
{
        private void BuildMachineSetupSection(IContainer c, ManufacturingOrderPdfDocument d, bool templateOnly)
        {
            c.Table(t =>
            {
                t.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(55);
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                });

                t.Cell().ColumnSpan(8).Element(CellHeaderTitle)
                    .AlignCenter()
                    .Text("Thông số cài đặt (yêu cầu trưởng ca SX phải cập nhật tốc độ, nhiệt độ máy đùn thực tế lúc máy đang SX)")
                    .FontSize(9).SemiBold();

                t.Cell().Element(CellHeader).Text("");
                t.Cell().Element(CellHeader).Text("Operator").SemiBold();
                t.Cell().Element(CellHeader).Text("Machine").SemiBold();
                t.Cell().Element(CellHeader).Text("Mfg Date").SemiBold();
                t.Cell().Element(CellHeader).Text("Batch #").SemiBold();
                t.Cell().Element(CellHeader).Text("Total Qty (kg)").SemiBold();
                t.Cell().Element(CellHeader).Text("Time (h)").SemiBold();
                t.Cell().Element(CellHeader).Text("Prod (kgs/h)").SemiBold();

                t.Cell().Element(CellPrev).Text("Previous").SemiBold();
                t.Cell().Element(CellBody).Text("");
                t.Cell().Element(CellBody).Text("");
                t.Cell().Element(CellBody).Text("");
                t.Cell().Element(CellBody).Text(templateOnly ? "" : d.Header?.FormulaExternalId);
                t.Cell().Element(CellBody).Text("");
                t.Cell().Element(CellBody).Text("");
                t.Cell().Element(CellBody).Text("");

                // Zone 1-6
                t.Cell().Element(CellPrev).Text("");
                t.Cell().Element(CellZoneHeader).Text("Rpm").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("Zone 1(°C)").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("Zone 2(°C)").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("Zone 3(°C)").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("Zone 4(°C)").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("Zone 5(°C)").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("Zone 6(°C)").SemiBold();

                t.Cell().Element(CellPrev).Text("Previous").SemiBold();
                for (int i = 0; i < 7; i++) t.Cell().Element(CellBody).Text("");

                // Zone 7-11 + Die
                t.Cell().Element(CellPrev).Text("");
                t.Cell().Element(CellZoneHeader).Text("Zone 7(°C)").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("Zone 8(°C)").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("Zone 9(°C)").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("Zone 10(°C)").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("Zone 11(°C)").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("Die-head(°C)").SemiBold();
                t.Cell().Element(CellZoneHeader).Text("");

                t.Cell().Element(CellPrev).Text("Previous").SemiBold();
                for (int i = 0; i < 7; i++) t.Cell().Element(CellBody).Text("");

                t.Cell().ColumnSpan(8).Element(CellNote)
                    .Text("Note/Lưu ý: tolerance temperature permitted up - down 10°C/nhiệt độ cài đặt được phép tăng giảm trong khoảng 10°C")
                    .FontSize(8);

                // signature table
                t.Cell().ColumnSpan(8).Element(SignatureSection);
            });

            // styles (giữ y hệt bạn đang có)
            static IContainer CellHeaderTitle(IContainer x) => x.Border(1).BorderColor(Colors.Black)
                .Background(Colors.Grey.Lighten3).PaddingVertical(4).PaddingHorizontal(4);

            static IContainer CellHeader(IContainer x) => x.Border(1).BorderColor(Colors.Black)
                .Background(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(3);

            static IContainer CellZoneHeader(IContainer x) => x.Border(1).BorderColor(Colors.Black)
                .Background(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(3).AlignCenter();

            static IContainer CellPrev(IContainer x) => x.Border(1).BorderColor(Colors.Black)
                .Background(Colors.Grey.Lighten3).PaddingVertical(3).PaddingHorizontal(3);

            static IContainer CellBody(IContainer x) => x.Border(1).BorderColor(Colors.Black)
                .PaddingVertical(3).PaddingHorizontal(3);

            static IContainer CellNote(IContainer x) => x.Border(1).BorderColor(Colors.Black)
                .PaddingVertical(3).PaddingHorizontal(3);



        }

        private static void SignatureSection(IContainer c)
        {
            c.Border(1).BorderColor(Colors.Black).Padding(0).Column(col =>
            {
                // header row
                col.Item().Row(r =>
                {
                    r.RelativeItem().Element(HeaderBox).Text("Thành phẩm").SemiBold();
                    r.RelativeItem().Element(HeaderBox).Text("Người tổng kết").SemiBold();
                    r.RelativeItem().Element(HeaderBox).Text("Check by (PL&PU)").SemiBold();
                    r.RelativeItem().Element(HeaderBox).Text("Approved").SemiBold();
                });

                // body: 1 hàng cao để ký (nhìn là biết ký ở đâu)
                col.Item().Height(70).Row(r =>
                {
                    r.RelativeItem().Element(BodyBox).Column(c1 =>
                    {
                        c1.Item().Text("Hàng đạt / Không đạt / Hao hụt").FontSize(8);

                        c1.Item().PaddingTop(6).Text("Hàng đạt").FontSize(8);
                        c1.Item().PaddingTop(2).Text("Hàng không đạt").FontSize(8);
                        c1.Item().PaddingTop(2).Text("Hao hụt").FontSize(8);
                    });

                    r.RelativeItem().Element(BodyBox).AlignCenter().AlignTop().Text("Ký & ghi rõ họ tên");
                    r.RelativeItem().Element(BodyBox).AlignCenter().AlignTop().Text("Ký & ghi rõ họ tên");
                    r.RelativeItem().Element(BodyBox).AlignCenter().AlignTop().Text("Ký & ghi rõ họ tên");
                });

                // date row (nhỏ thôi)
                col.Item().Row(r =>
                {
                    r.RelativeItem().Element(DateBox).Text("").FontSize(8);
                    r.RelativeItem().Element(DateBox).Text("Date: ....../....../......").FontSize(8);
                    r.RelativeItem().Element(DateBox).Text("Date: ....../....../......").FontSize(8);
                    r.RelativeItem().Element(DateBox).Text("Date: ....../....../......").FontSize(8);
                });
            });

            static IContainer HeaderBox(IContainer x) =>
                x.BorderRight(1).BorderColor(Colors.Black)
                 .Background(Colors.Grey.Lighten2)
                 .PaddingVertical(3).PaddingHorizontal(4);

            static IContainer BodyBox(IContainer x) =>
                x.BorderRight(1).BorderColor(Colors.Black)
                 .BorderTop(1).BorderColor(Colors.Black)
                 .Padding(6);

            static IContainer DateBox(IContainer x) =>
                x.BorderRight(1).BorderColor(Colors.Black)
                 .BorderTop(1).BorderColor(Colors.Black)
                 .PaddingVertical(3).PaddingHorizontal(4);
        }
}
