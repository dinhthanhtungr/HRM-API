using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;

namespace HRM.Infrastructure.Documents.Pdfs
{
    internal sealed class PurchaseOrderHeader(string contentRoot, string? logoPath) : IComponent
    {
        public void Compose(IContainer container)
        {
            container.Column(column =>
            {

                column.Item().Row(row =>
                {
                    row.RelativeItem(1).Height(60).AlignMiddle().AlignLeft().Element(e =>
                    {
                        var imagePath = string.IsNullOrWhiteSpace(logoPath) ? Path.Combine(contentRoot, "Assets", "Pdf", "VietAusLogo.png") : Path.GetFullPath(logoPath, contentRoot);
                        if (File.Exists(imagePath))
                            e.Image(Image.FromFile(imagePath)).FitHeight();
                        else
                            e.Text("Logo").FontSize(10);
                    });

                    row.RelativeItem(1).AlignMiddle().Element(e =>
                    {
                        e.Border(1)
                         .PaddingLeft(5) // ← chỉ cần Padding trái
                         .PaddingBottom(2)
                         .PaddingTop(2) // thêm nếu muốn cân đối
                         .Column(col =>
                         {

                             col.Item().Row(row =>
                             {
                                 row.RelativeItem(1).Text("Factory 01:")
                                     .FontFamily("Open Sans").FontSize(7);
                                 row.RelativeItem(3).Text("No.296, Trung Thang, Dong Hoa ward,\nHo Chi Minh city, Vietnam.")
                                     .FontFamily("Open Sans").FontSize(7);
                             });

                             col.Item().Row(row =>
                             {
                                 row.RelativeItem(1).Text("Factory 02:")
                                     .FontFamily("Open Sans").FontSize(7);
                                 row.RelativeItem(3).Text("Industrial park slope 47, Long Khanh 2 quarter,\nTam Phuoc ward, Dong Nai city, Vietnam.")
                                     .FontFamily("Open Sans").FontSize(7);
                             });

                             col.Item().Row(row =>
                             {
                                 row.RelativeItem(1).Text("Tel:")
                                     .FontFamily("Open Sans").FontSize(7);
                                 row.RelativeItem(3).Text("(84). 28. 730 93 969")
                                     .FontFamily("Open Sans").FontSize(7);
                             });

                             col.Item().Row(row =>
                             {
                                 row.RelativeItem(1).Text("Website: ")
                                     .FontFamily("Open Sans").FontSize(7).FontColor(Colors.Blue.Medium);
                                 row.RelativeItem(3).Text("https://vietaus.com")
                                     .FontFamily("Open Sans").FontSize(7).FontColor(Colors.Blue.Medium);
                             });
                         });
                    });

                });

                column.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);
            });
        }
    }

}
