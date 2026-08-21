using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HRM.Infrastructure.Documents.Pdfs;

namespace HRM.Infrastructure.Documents.Pdfs.Components;

internal sealed class IsoPdfFooterComponent : IComponent
{
    private readonly QuotationPdfBrandingOptions _options;
    private readonly byte[]? _bureauVeritasLogo;
    private readonly byte[]? _grsLogo;
    private readonly byte[]? _qrCode;
    private readonly string? _formCode;
    private readonly string? _effectiveDate;

    public IsoPdfFooterComponent(
        QuotationPdfBrandingOptions options,
        byte[]? bureauVeritasLogo,
        byte[]? grsLogo,
        byte[]? qrCode,
        string? formCode = null,
        string? effectiveDate = null)
    {
        _options = options;
        _bureauVeritasLogo = bureauVeritasLogo;
        _grsLogo = grsLogo;
        _qrCode = qrCode;
        _formCode = formCode ?? options.FormCode;
        _effectiveDate = effectiveDate ?? options.EffectiveDate;
    }

    public void Compose(IContainer container)
    {
        container.PaddingTop(PdfLayout.FooterTopPadding).Column(column =>
        {
            column.Item()
                .LineHorizontal(PdfLayout.DividerWidth)
                .LineColor(PdfColors.BorderGrey);
            column.Item().PaddingTop(PdfLayout.FooterTextTopPadding).AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(PdfTypography.Small);
                text.Span("Website: ");
                text.Span(_options.Website).FontColor(PdfColors.LinkBlue);
                text.Span($" - hotline: {_options.Hotline}");
            });
            column.Item().AlignCenter().Text(_options.Slogan).Bold().FontSize(PdfTypography.SmallSize);

            if (HasCertificationImage())
            {
                column.Item().PaddingTop(PdfLayout.FooterImageTopPadding).AlignCenter().Row(ComposeCertificationImages);
            }

            column.Item().PaddingTop(PdfLayout.FooterPageTopPadding).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(_formCode ?? string.Empty).FontSize(PdfTypography.FooterSize);
                row.RelativeItem().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(PdfTypography.Footer);
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
                row.RelativeItem().AlignRight().Text(_effectiveDate ?? string.Empty).FontSize(PdfTypography.FooterSize);
            });
        });
    }

    private bool HasCertificationImage()
        => _bureauVeritasLogo is { Length: > 0 } ||
           _grsLogo is { Length: > 0 } ||
           _qrCode is { Length: > 0 };

    private void ComposeCertificationImages(RowDescriptor row)
    {
        row.RelativeItem();

        AddImage(row, _bureauVeritasLogo, PdfLayout.BureauVeritasLogoWidth);
        AddImage(row, _grsLogo, PdfLayout.GrsLogoWidth);
        AddImage(row, _qrCode, PdfLayout.QrCodeWidth);

        row.RelativeItem();
    }

    private static void AddImage(RowDescriptor row, byte[]? image, float width)
    {
        if (image is not { Length: > 0 })
        {
            return;
        }

        row.ConstantItem(width)
            .PaddingHorizontal(PdfLayout.CertificationImagePaddingHorizontal)
            .Height(PdfLayout.CertificationImageHeight)
            .AlignMiddle().Image(image).FitArea();
    }
}
