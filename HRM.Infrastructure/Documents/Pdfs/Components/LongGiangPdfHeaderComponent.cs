using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs.Components;

internal sealed class LongGiangPdfHeaderComponent(byte[]? logo) : IComponent
{
    public void Compose(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem(1).Height(50).AlignMiddle().AlignLeft().Element(ComposeLogo);
                row.RelativeItem(2).AlignRight().AlignMiddle().Border(1)
                    .PaddingLeft(5).PaddingRight(5).PaddingBottom(5).Column(company =>
                    {
                        company.Item().Text(
                                "Address: No 26/6, Street 12, Tam Binh ward,\nThu Duc City, Ho Chi Minh City, Vietnam.")
                            .FontSize(7);
                        company.Item().Text("Tel: (84). 932. 66 36 89     Fax: (84) 28. 37 29 29 64")
                            .FontSize(7);
                        company.Item().Text("Website: www.lgplastic.com")
                            .FontSize(7).FontColor(Colors.Blue.Medium);
                    });
            });

            column.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);
        });
    }

    private void ComposeLogo(IContainer container)
    {
        if (logo is { Length: > 0 })
        {
            container.Image(logo).FitHeight();
            return;
        }

        container.Text("Logo").FontSize(10);
    }
}
