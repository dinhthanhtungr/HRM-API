using HRM.Application.Features.PLM.SampleRequests.SampleTrials;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestSampleTrialMutationRulesTests
{
    [Fact]
    public void Validate_RejectsNegativeQuantities()
    {
        Assert.NotNull(SampleRequestSampleTrialMutationRules.Validate(-1, null, null, null, null));
        Assert.NotNull(SampleRequestSampleTrialMutationRules.Validate(null, -0.1m, null, null, null));
    }

    [Fact]
    public void Validate_RejectsInvalidDateOrder()
    {
        var received = new DateTime(2026, 6, 10);
        var finished = new DateTime(2026, 6, 9);
        var sent = new DateTime(2026, 6, 8);

        Assert.NotNull(SampleRequestSampleTrialMutationRules.Validate(null, null, received, finished, null));
        Assert.NotNull(SampleRequestSampleTrialMutationRules.Validate(null, null, null, received, sent));
    }

    [Fact]
    public void PatchContract_AllowsCaseInsensitiveClearField()
    {
        var result = SampleRequestSampleTrialPatchContract.ValidateAndNormalize(
            ["LABNOTE"],
            Array.Empty<string>());

        Assert.True(result.Success);
        Assert.True(result.Data!.Contains(SampleRequestSampleTrialPatchFields.LabNote));
    }

    [Fact]
    public void PatchContract_RejectsUnknownClearField()
    {
        var result = SampleRequestSampleTrialPatchContract.ValidateAndNormalize(
            ["unknownField"],
            Array.Empty<string>());

        Assert.False(result.Success);
    }

    [Fact]
    public void PatchContract_RejectsUpdateAndClearConflict()
    {
        var result = SampleRequestSampleTrialPatchContract.ValidateAndNormalize(
            [SampleRequestSampleTrialPatchFields.LabNote],
            [SampleRequestSampleTrialPatchFields.LabNote]);

        Assert.False(result.Success);
    }
}
