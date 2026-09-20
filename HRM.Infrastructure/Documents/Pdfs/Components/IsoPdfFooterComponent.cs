using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HRM.Infrastructure.Documents.Pdfs;
using HRM.Application.Features.CRM.Quotations.Dtos;

namespace HRM.Infrastructure.Documents.Pdfs.Components;

internal sealed class IsoPdfFooterComponent : IComponent
{
    private readonly QuotationPdfBrandingOptions _options;
    private readonly byte[]? _bureauVeritasLogo;
    private readonly byte[]? _grsLogo;
    private readonly byte[]? _qrCode;
    private readonly QuotationPdfDocumentDto? _quotation;
    private readonly string? _formCode;
    private readonly string? _effectiveDate;

    public IsoPdfFooterComponent(
        QuotationPdfBrandingOptions options,
        byte[]? bureauVeritasLogo,
        byte[]? grsLogo,
        byte[]? qrCode,
        string? formCode = null,
        string? effectiveDate = null)
        : this(options, bureauVeritasLogo, grsLogo, qrCode, null, formCode, effectiveDate)
    {
    }

    public IsoPdfFooterComponent(
        QuotationPdfBrandingOptions options,
        byte[]? bureauVeritasLogo,
        byte[]? grsLogo,
        byte[]? qrCode,
        QuotationPdfDocumentDto quotation)
        : this(options, bureauVeritasLogo, grsLogo, qrCode, quotation, null, null)
    {
    }

    private IsoPdfFooterComponent(
        QuotationPdfBrandingOptions options,
        byte[]? bureauVeritasLogo,
        byte[]? grsLogo,
        byte[]? qrCode,
        QuotationPdfDocumentDto? quotation,
        string? formCode,
        string? effectiveDate)
    {
        _options = options;
        _bureauVeritasLogo = bureauVeritasLogo;
        _grsLogo = grsLogo;
        _qrCode = qrCode;
        _quotation = quotation;
        _formCode = formCode ?? options.FormCode;
        _effectiveDate = effectiveDate ?? options.EffectiveDate;
    }

    public void Compose(IContainer container)
    {
        container.PaddingTop(PdfLayout.FooterTopPadding).Column(column =>
        {
            if (_quotation is not null)
            {
                ComposeCompanyContacts(column);
            }

            column.Item()
                .PaddingTop(2)
                .LineHorizontal(PdfLayout.DividerWidth)
                .LineColor(PdfColors.BorderGrey);
            column.Item().PaddingTop(PdfLayout.FooterTextTopPadding).AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(PdfTypography.Small);
                text.Span("Web: ").Bold().Italic().FontColor(PdfColors.LinkBlue);
                text.Span(FormatWebsite(_options.Website)).Bold().Italic().FontColor(PdfColors.LinkBlue);
                text.Span($" - hotline: {_options.Hotline}").Bold().Italic().FontColor(PdfColors.LinkBlue);
            });
            column.Item().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(PdfTypography.Small);
                if (string.Equals(
                        _options.Slogan,
                        "COLOURING YOUR FUTURE WITH SERVICE AT YOUR DOORSTEP",
                        StringComparison.OrdinalIgnoreCase))
                {
                    text.Span("COLOURING ").Bold().FontColor(PdfColors.TitleRed);
                    text.Span("YOUR FUTURE ").Bold().FontColor(PdfColors.TableHeaderYellow);
                    text.Span("WITH SERVICE ").Bold().FontColor(PdfColors.BrandGreen);
                    text.Span("AT YOUR DOORSTEP").Bold().FontColor(PdfColors.LinkBlue);
                }
                else
                {
                    text.Span(_options.Slogan).Bold().FontColor(PdfColors.LinkBlue);
                }
            });

            if (HasCertificationImage())
            {
                column.Item().PaddingTop(PdfLayout.FooterImageTopPadding).AlignCenter().Row(ComposeCertificationImages);
            }

            column.Item().PaddingTop(PdfLayout.FooterPageTopPadding).Row(row =>
            {
                if (_quotation is not null)
                {
                    row.RelativeItem().AlignLeft().Text(_options.IsoStandards).FontSize(PdfTypography.FooterSize);
                    row.RelativeItem().AlignCenter().Text(_effectiveDate ?? string.Empty).FontSize(PdfTypography.FooterSize);
                    row.RelativeItem().AlignRight().Text(_formCode ?? string.Empty).FontSize(PdfTypography.FooterSize);
                }
                else
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
                }
            });
        });
    }

    private void ComposeCompanyContacts(ColumnDescriptor column)
    {
        column.Item().PaddingTop(2).Text(_options.CompanyDisplayName)
            .Bold().FontColor(PdfColors.LinkBlue).FontSize(PdfTypography.SmallSize);

        AddOfficeRow(
            column,
            ("Head office", _options.HeadOffice, _options.HeadOfficeContact, _options.HeadOfficeEmail),
            ("Branch Ha Noi", _options.HaNoiBranch, _options.HaNoiBranchContact, _options.HaNoiBranchEmail));
        AddOfficeRow(
            column,
            ("Factory I", _options.Factory01, _options.Factory01Contact, _options.Factory01Email),
            ("Branch Da Nang", _options.DaNangBranch, _options.DaNangBranchContact, _options.DaNangBranchEmail));
        AddOfficeRow(
            column,
            ("Factory II", _options.Factory02, _options.Factory02Contact, _options.Factory02Email),
            null);
    }

    private static void AddOfficeRow(
        ColumnDescriptor column,
        (string Label, string? Address, string Contact, string Email) left,
        (string Label, string? Address, string Contact, string Email)? right)
    {
        column.Item().PaddingTop(2).Row(row =>
        {
            row.RelativeItem().Element(container => ComposeOffice(container, left));
            row.ConstantItem(12);
            row.RelativeItem().Element(container =>
            {
                if (right is not null)
                {
                    ComposeOffice(container, right.Value);
                }
            });
        });
    }

    private static void ComposeOffice(
        IContainer container,
        (string Label, string? Address, string Contact, string Email) office)
    {
        container.Column(column =>
        {
            column.Item().Text(text =>
            {
                text.DefaultTextStyle(PdfTypography.Footer);
                text.Span($"{office.Label}: ").Bold();
                text.Span(office.Address ?? string.Empty);
            });
            column.Item().Text(office.Contact).FontSize(PdfTypography.FooterSize);
            column.Item().Text(text =>
            {
                text.DefaultTextStyle(PdfTypography.Footer);
                text.Span("Email: ");
                text.Span(office.Email).FontColor(PdfColors.LinkBlue).Underline();
            });
        });
    }

    private static string FormatWebsite(string website)
    {
        var host = website.Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
        return string.Equals(host, "vietaus.com", StringComparison.OrdinalIgnoreCase)
            ? "www.vietaus.com"
            : host;
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
