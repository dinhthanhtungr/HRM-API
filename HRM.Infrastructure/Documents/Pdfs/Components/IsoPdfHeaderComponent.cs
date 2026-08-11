using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HRM.Infrastructure.Documents.Pdfs;

namespace HRM.Infrastructure.Documents.Pdfs.Components;

internal sealed class IsoPdfHeaderComponent : IComponent
{
    private readonly string? _companyAddress;
    private readonly string? _companyPhone;
    private readonly string? _factory01;
    private readonly string? _factory02;
    private readonly string _website;
    private readonly byte[]? _logo;

    public IsoPdfHeaderComponent(
        string? companyAddress,
        string? companyPhone,
        QuotationPdfBrandingOptions options,
        byte[]? logo)
    {
        _companyAddress = companyAddress;
        _companyPhone = companyPhone;
        _factory01 = options.Factory01;
        _factory02 = options.Factory02;
        _website = options.Website;
        _logo = logo;
    }

    public void Compose(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem(1).Height(PdfLayout.HeaderLogoHeight).AlignMiddle().AlignLeft().Element(ComposeLogo);
                row.RelativeItem(1).AlignMiddle().Border(PdfLayout.TableBorderWidth)
                    .PaddingLeft(PdfLayout.HeaderInfoPaddingLeft)
                    .PaddingTop(PdfLayout.HeaderInfoPaddingVertical)
                    .PaddingBottom(PdfLayout.HeaderInfoPaddingVertical)
                    .Column(company =>
                    {
                        AddInformationRow(company, "Factory 01:", _factory01 ?? _companyAddress);
                        AddInformationRow(company, "Factory 02:", _factory02);
                        AddInformationRow(company, "Tel:", _companyPhone);
                        AddInformationRow(company, "Website:", _website, PdfColors.LinkBlue);
                    });
            });

            column.Item()
                .PaddingTop(PdfLayout.HeaderDividerTopPadding)
                .LineHorizontal(PdfLayout.DividerWidth)
                .LineColor(PdfColors.DividerGrey);
        });
    }

    private void ComposeLogo(IContainer container)
    {
        if (_logo is { Length: > 0 })
        {
            container.Image(_logo).FitHeight();
            return;
        }

        container.Text("Logo").FontSize(PdfTypography.LogoFallbackSize);
    }

    private static void AddInformationRow(
        ColumnDescriptor column,
        string label,
        string? value,
        string? color = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        column.Item().Row(row =>
        {
            row.RelativeItem(1).Text(label).FontSize(PdfTypography.SmallSize);
            var text = row.RelativeItem(3).Text(value).FontSize(PdfTypography.SmallSize);
            if (color is not null)
            {
                text.FontColor(color);
            }
        });
    }
}
