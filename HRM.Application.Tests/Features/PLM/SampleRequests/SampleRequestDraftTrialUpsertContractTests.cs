using HRM.Application.Features.PLM.SampleRequests.SampleTrials;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestDraftTrialUpsertContractTests
{
    [Fact]
    public void ClearFields_RejectsFieldsOutsideDraftTechnicalScope()
    {
        var result = SampleRequestDraftTrialUpsertContract.ValidateAndNormalizeClearFields(
            [SampleRequestSampleTrialPatchFields.CustomerReplyNote],
            Array.Empty<string>());

        Assert.False(result.Success);
    }

    [Fact]
    public void HasTrialMutation_ReturnsFalseForParentDatesOnly()
    {
        var result = SampleRequestDraftTrialUpsertContract.HasTrialMutation(
            [SampleRequestSampleTrialPatchFields.ExpectedDeliveryDate],
            Array.Empty<string>());

        Assert.False(result);
    }

    [Fact]
    public void HasTrialMutation_ReturnsTrueForLabNote()
    {
        var result = SampleRequestDraftTrialUpsertContract.HasTrialMutation(
            [SampleRequestSampleTrialPatchFields.LabNote],
            Array.Empty<string>());

        Assert.True(result);
    }

    [Fact]
    public void ClearFields_RejectsAdditiveRateBecauseUsageRateBelongsToProduct()
    {
        var result = SampleRequestDraftTrialUpsertContract.ValidateAndNormalizeClearFields(
            [SampleRequestSampleTrialPatchFields.AdditiveRate],
            Array.Empty<string>());

        Assert.False(result.Success);
    }

    [Fact]
    public void ClearFields_RejectsUpdateAndClearConflict()
    {
        var result = SampleRequestDraftTrialUpsertContract.ValidateAndNormalizeClearFields(
            [SampleRequestSampleTrialPatchFields.LabNote],
            [SampleRequestSampleTrialPatchFields.LabNote]);

        Assert.False(result.Success);
    }
}
