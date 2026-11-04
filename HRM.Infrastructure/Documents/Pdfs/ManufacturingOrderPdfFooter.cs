
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs
{
    public class ManufacturingOrderPdfFooter : IComponent
    {
        public void Compose(IContainer container)
        {
            container.PaddingTop(20).Column(col =>
            {


                // Website + Slogan 
                col.Item().PaddingTop(10).Row(row =>
                {
                    // Website và Slogan
                    row.RelativeItem().AlignCenter().AlignMiddle().Column(c =>
                    {
                        c.Item().AlignCenter().AlignMiddle().Element(e => e.Text(txt =>
                        {
                            txt.Span("Website: ");
                            txt.Span("https://vietaus.com").FontColor(Colors.Blue.Medium);
                            txt.Span(" - hotline: (84). 8. 73 09 39 69");
                        }));

                        c.Item().AlignCenter().AlignMiddle().Element(e => e.Text("COLOURING YOUR FUTURE WITH SERVICE AT YOUR DOORSTEP")
                            .Bold().FontFamily("Open Sans").FontSize(9));
                    });

                });

                // Logo 17ece7c7-9afc-478d-0acd-08ddbe89253f
                //col.Item().PaddingTop(10).Row(row =>
                //{
                //    var imagePath = "wwwroot/images/Iso/bureau-veritas.png";
                //    var imageQrPath = "wwwroot/images/Iso/QR.png";
                //    var imageGrsPath = "wwwroot/images/Iso/GRS.png";

                //    row.RelativeItem().AlignCenter().AlignMiddle().Element(e => e.Row(innerRow =>
                //    {
                //        if (File.Exists(imagePath))
                //        {
                //            innerRow.ConstantItem(80).PaddingRight(10).Element(img =>
                //            {
                //                img.AlignMiddle().Image(imagePath).FitWidth();
                //            });
                //        }

                //        if (File.Exists(imageGrsPath))
                //        {
                //            innerRow.ConstantItem(80).PaddingRight(10).Element(img =>
                //            {
                //                img.AlignMiddle().Image(imageGrsPath).FitWidth();
                //            });
                //        }

                //        if (File.Exists(imageQrPath))
                //        {
                //            innerRow.ConstantItem(45).PaddingRight(10).Element(img =>
                //            {
                //                img.AlignMiddle().Image(imageQrPath).FitWidth();
                //            });
                //        }


                //    }));
                //});


                col.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem()
                        .AlignLeft()
                        .Element(e => e.Text("VA-PL&PU-F02(04)")
                            .FontFamily("Open Sans")
                            .FontSize(9));

                    //row.RelativeItem()
                    //    .AlignCenter()
                    //    .Element(e => e.Text("25-03-2026")
                    //        .FontFamily("Open Sans")
                    //        .FontSize(9));

                    row.RelativeItem()
                        .AlignRight()
                        .Element(e => e.Text("25-03-2026")
                            .FontFamily("Open Sans")
                            .FontSize(9));
                });
            });
        }
    }
}
