using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs;

internal static class PdfLayout
{
    public const float PageMarginHorizontal = 24;
    public const float PageMarginVertical = 18;
    public const float ContentTopPadding = 6;
    public const float QuotationColumnSpacing = 5;
    public const float SectionDividerWidth = 2;
    public const float DividerWidth = 1;
    public const float ThinBorderWidth = 0.5f;
    public const float TableBorderWidth = 1;
    public const float EmptyStatePadding = 8;
    public const float SignatureTopPadding = 4;
    public const float TableCellPaddingVertical = 3;
    public const float TableCellPaddingHorizontal = 2;
    public const float HeaderLogoHeight = 60;
    public const float HeaderInfoPaddingLeft = 5;
    public const float HeaderInfoPaddingVertical = 2;
    public const float HeaderDividerTopPadding = 5;
    public const float FooterTopPadding = 6;
    public const float FooterTextTopPadding = 4;
    public const float FooterImageTopPadding = 3;
    public const float FooterPageTopPadding = 3;
    public const float CertificationImageHeight = 24;
    public const float CertificationImagePaddingHorizontal = 3;
    public const float BureauVeritasLogoWidth = 52;
    public const float GrsLogoWidth = 52;
    public const float QrCodeWidth = 32;

    public static PageSize A4(bool landscape)
        => landscape ? PageSizes.A4.Landscape() : PageSizes.A4;
}
