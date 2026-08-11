using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs;

internal static class PdfTypography
{
    public const string FontFamily = "Open Sans";

    public const float FooterSize = 6;
    public const float SmallSize = 7;
    public const float BodySize = 8;
    public const float LogoFallbackSize = 10;
    public const float QuotationTitleSize = 16;

    public static TextStyle Body(TextStyle style)
        => style.FontFamily(FontFamily)
            .FontSize(BodySize)
            .FontColor(Colors.Black);

    public static TextStyle Small(TextStyle style)
        => style.FontFamily(FontFamily)
            .FontSize(SmallSize);

    public static TextStyle Footer(TextStyle style)
        => style.FontFamily(FontFamily)
            .FontSize(FooterSize);
}
