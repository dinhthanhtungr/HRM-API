using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Rules;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestPriceConfirmationBasisTests
{
    [Fact]
    public void NewConfirmedFormulaWinsOverOlderSentTrialWithoutChangingTrialDisplay()
    {
        var oldTrialFormula = Guid.NewGuid();
        var confirmed = Guid.NewGuid();
        var item = new SampleRequestSampleTrialReportDto { FormulaId = oldTrialFormula, PriceConfirmationFormulaId = confirmed };
        Assert.True(SampleRequestPriceConfirmationBasis.MatchesApprovedFormula(item, confirmed));
        Assert.False(SampleRequestPriceConfirmationBasis.MatchesApprovedFormula(item, oldTrialFormula));
        Assert.Equal(oldTrialFormula, item.FormulaId);
    }

    [Fact]
    public void ConfirmedFormulaCanBeUsedBeforeCustomerSelectionOrAssignedTrial()
    {
        var confirmed = Guid.NewGuid();
        var item = new SampleRequestSampleTrialReportDto { PriceConfirmationFormulaId = confirmed };
        Assert.Equal(confirmed, SampleRequestPriceConfirmationBasis.ResolveFormulaId(item));
        Assert.True(SampleRequestPriceConfirmationBasis.MatchesApprovedFormula(item, confirmed));
    }

    [Fact]
    public void LegacyFormulaFallbackDoesNotTreatMissingSourceAsApproved()
    {
        var item = new SampleRequestSampleTrialReportDto();
        Assert.False(SampleRequestPriceConfirmationBasis.MatchesApprovedFormula(item, null));
        item.FormulaId = Guid.NewGuid();
        Assert.True(SampleRequestPriceConfirmationBasis.MatchesApprovedFormula(item, item.FormulaId));
    }
}
