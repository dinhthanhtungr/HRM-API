using HRM.Application.Features.PLM.Materials.Queries.GetFormulaItemLookup;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class ProcessingLookupSearchTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("  ", null, null)]
    [InlineData("009", "009", null)]
    [InlineData("NVL_BM_009", "NVL_BM_009", null)]
    [InlineData("009 Băm", "009", "B")]
    [InlineData("009 BAM", "009", "B")]
    [InlineData("  009   NGHIỀN  ", "009", "N")]
    [InlineData("009 nghien", "009", "N")]
    [InlineData("NVL_NH_009-N", "NVL_NH_009", "N")]
    [InlineData("NVL_NH_009_B", "NVL_NH_009", "B")]
    [InlineData("Nghiền", null, "N")]
    [InlineData("Bamboo", "Bamboo", null)]
    [InlineData("009%_ Băm", "009%_", "B")]
    public void OnlyExplicitQualifierEnablesProcessing(string? input, string? keyword, string? type)
    {
        Assert.Equal((keyword, type), ProcessingLookupSearch.Parse(input));
    }
}
