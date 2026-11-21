using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.PLM.SampleRequests.Commands.FillProductColourNames;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class ProductColourNamePreviewExcelTests
{
    [Fact]
    public void ExportsEveryCandidateAsTextIncludingSkippedAndFormulaLikeNames()
    {
        var type = typeof(HRM.Infrastructure.DatabaseContext.ApplicationDbs.ApplicationDbContext).Assembly
            .GetType("HRM.Infrastructure.Documents.Excels.ProductColourNamePreviewExcelRenderer")!;
        var renderer = (IProductColourNamePreviewExcelRenderer)Activator.CreateInstance(type, nonPublic: true)!;
        var items = Enumerable.Range(0, 120).Select(i => new ProductColourNameCandidate(Guid.NewGuid(),
            i == 0 ? "=1+1" : "BLACK MB", i == 1 ? null : "Black", "CODE")).ToArray();
        using var stream = new MemoryStream(renderer.Render(new(true, 120, 119, 0, 0, items, false)));
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        Assert.Equal(121, sheet.LastRowUsed()!.RowNumber());
        Assert.False(sheet.Cell(2, 3).HasFormula);
        Assert.Equal("=1+1", sheet.Cell(2, 3).GetString());
        Assert.Equal("Black", sheet.Cell(2, 5).GetString());
        Assert.Contains("Bỏ qua", sheet.Cell(3, 6).GetString());
    }
}
