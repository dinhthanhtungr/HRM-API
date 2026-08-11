using QuestPDF.Helpers;

namespace HRM.Infrastructure.Documents.Pdfs;

internal static class PdfColors
{
    public const string BrandGreen = "#008A2E";
    public const string TableHeaderYellow = "#FFF200";

    public static string TitleRed => Colors.Red.Medium;
    public static string LinkBlue => Colors.Blue.Medium;
    public static string BorderGrey => Colors.Grey.Lighten1;
    public static string DividerGrey => Colors.Grey.Medium;
}
