namespace HRM.Infrastructure.Documents.Pdfs;

public sealed class PdfOptions
{
    public const string SectionName = "Pdf";

    public string QuestPdfLicense { get; init; } = "Evaluation";
    public QuotationPdfBrandingOptions Quotation { get; init; } = new();
}

public sealed class QuotationPdfBrandingOptions
{
    public string? LogoPath { get; init; }
    public string? BureauVeritasLogoPath { get; init; }
    public string? GrsLogoPath { get; init; }
    public string? QrCodePath { get; init; }

    public string? Factory01 { get; init; }
    public string? Factory02 { get; init; }
    public string Website { get; init; } = "https://vietaus.com";
    public string Hotline { get; init; } = "(84). 28. 730 93 969";
    public string Slogan { get; init; } =
        "COLOURING YOUR FUTURE WITH SERVICE AT YOUR DOORSTEP";

    public string? FormCode { get; init; }
    public string? EffectiveDate { get; init; }
}
