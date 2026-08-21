using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs.Components;

internal sealed class LongGiangPdfFooterComponent(
    string documentCode,
    string revisionDate) : IComponent
{
    public void Compose(IContainer container)
    {
        container.PaddingTop(20).Column(column =>
        {
            column.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Text(documentCode).FontSize(9);
                row.ConstantItem(100).AlignRight().Text(revisionDate).FontSize(9);
            });
        });
    }
}
