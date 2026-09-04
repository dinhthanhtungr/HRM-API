using HRM.Application.Features.PLM.ColorChipRecords.Services;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.ColorChipRecords;

public sealed class ColorChipRecordContractTests
{
    [Fact]
    public void PatchContract_RejectsUnsupportedClearField()
    {
        var result = ColorChipRecordPatchContract.ValidateAndNormalize(
            ["companyId"],
            []);

        Assert.False(result.Success);
        Assert.Contains("companyId", result.Message);
    }

    [Fact]
    public void PatchContract_RejectsUpdateAndClearConflict()
    {
        var result = ColorChipRecordPatchContract.ValidateAndNormalize(
            [ColorChipRecordPatchFields.Note],
            [ColorChipRecordPatchFields.Note]);

        Assert.False(result.Success);
        Assert.Contains(ColorChipRecordPatchFields.Note, result.Message);
    }

    [Fact]
    public void PatchContract_NormalizesFieldNamesCaseInsensitively()
    {
        var result = ColorChipRecordPatchContract.ValidateAndNormalize(
            ["PrintNote"],
            []);

        Assert.True(result.Success);
        Assert.Contains(ColorChipRecordPatchFields.PrintNote, result.Data!);
    }

    [Fact]
    public void DevelopmentFormulaIds_RejectsMoreThanOneFormula()
    {
        var result = ColorChipRecordRules.ValidateDevelopmentFormulaIds(
            [Guid.NewGuid(), Guid.NewGuid()]);

        Assert.False(result.Success);
    }

    [Fact]
    public void DevelopmentFormulaIds_EmptyListMeansClear()
    {
        var result = ColorChipRecordRules.ValidateDevelopmentFormulaIds([]);

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public void Measurements_RejectNegativePelletWeight()
    {
        Assert.NotNull(ColorChipRecordRules.ValidateMeasurements(-0.01m));
        Assert.Null(ColorChipRecordRules.ValidateMeasurements(0m));
    }

    [Theory]
    [InlineData(FormStyle.Chips5Options, 5)]
    [InlineData(FormStyle.ChipsTanPhuBacNinh3Thresholds, 6)]
    public void LegacyPrintLayouts_KeepStableNumericCodes(FormStyle formStyle, int expectedCode)
    {
        Assert.Equal(expectedCode, (int)formStyle);
    }
}
