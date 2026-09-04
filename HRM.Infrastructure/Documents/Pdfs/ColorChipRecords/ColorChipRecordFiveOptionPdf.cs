using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Linq;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords
{
    public class ColorChipRecordFiveOptionPdf : ColorChipRecordLandscapePdf, IColorChipRecordFiveOptionPdf
    {
        protected override void BuildFooter(IContainer c, ColorChipRecordPdfModel m)
        {
            var options = string.IsNullOrWhiteSpace(m.BatchNo)
                ? new[] { "OPTION 1", "OPTION 2", "OPTION 3", "OPTION 4", "OPTION 5" }
                : BuildOptions(m);

            c.PaddingTop(2)
             .PaddingBottom(2)
             .Row(row =>
             {
                 foreach (var option in options)
                 {
                     row.RelativeItem()
                        .AlignCenter()
                        .Text(option)
                        .FontSize(8.5f)
                        .Bold();
                 }
             });
        }

        private static string[] BuildOptions(ColorChipRecordPdfModel model)
        {
            var formulaCodes = model.DevelopmentFormulaCodes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(5)
                .ToList();

            if (formulaCodes.Count > 0)
            {
                while (formulaCodes.Count < 5)
                    formulaCodes.Add($"OPTION {formulaCodes.Count + 1}");

                return formulaCodes.ToArray();
            }

            var option2 = string.IsNullOrWhiteSpace(model.LowerText) ? "LOWER" : model.LowerText;
            var option3 = string.IsNullOrWhiteSpace(model.StandardText) ? "STANDARD" : model.StandardText;
            var option4 = string.IsNullOrWhiteSpace(model.UpperText) ? "UPPER" : model.UpperText;

            return new[]
            {
                model.BatchNo,
                option2,
                option3,
                option4,
                "OPTION 5"
            };
        }
    }
}
