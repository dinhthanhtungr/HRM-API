using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Application.Features.Pdf.TestDocuments;

public static class VietAusLogoTestPdf
{
    public static byte[] Generate(byte[] logoBytes)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Content()
                    .Column(column =>
                    {
                        column.Spacing(20);

                        column.Item()
                            .AlignCenter()
                            .Width(220)
                            .Image(logoBytes);

                        column.Item()
                            .AlignCenter()
                            .Text("VietAus FDI")
                            .FontSize(24)
                            .Bold();

                        column.Item()
                            .AlignCenter()
                            .Text("PDF test generated with QuestPDF.");

                        column.Item()
                            .PaddingTop(20)
                            .LineHorizontal(1)
                            .LineColor(Colors.Grey.Lighten2);
                    });
            });
        }).GeneratePdf();
    }
}
