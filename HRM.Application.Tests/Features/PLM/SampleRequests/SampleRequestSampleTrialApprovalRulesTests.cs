using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestSampleTrialApprovalRulesTests
{
    [Fact]
    public void MissingTrialIsOptionalOnlyForExplicitLegacySelection()
    {
        var formulaId = Guid.NewGuid();
        Assert.Null(SampleRequestSampleTrialApprovalRules.Validate(null, formulaId, allowMissingLegacyTrial: true));
        Assert.NotNull(SampleRequestSampleTrialApprovalRules.Validate(null, formulaId));
    }

    [Fact]
    public void Validate_RejectsFormulaThatDoesNotMatchLatestPendingTrial()
    {
        var sentFormula = CreateFormula();
        var trial = CreatePendingTrial(sentFormula);

        var result = SampleRequestSampleTrialApprovalRules.Validate(
            trial,
            Guid.NewGuid());

        Assert.Contains("does not match", result);
    }

    [Fact]
    public void ApplyApproved_CompletesTrialFormulaAndSampleRequestTogether()
    {
        var employeeId = Guid.NewGuid();
        var now = new DateTime(2026, 8, 14, 15, 30, 0, DateTimeKind.Local);
        var selectedFormula = CreateFormula();
        var otherFormula = CreateFormula();
        otherFormula.ProductId = selectedFormula.ProductId;
        otherFormula.IsSelect = true;
        var trial = CreatePendingTrial(selectedFormula);
        var sampleRequest = new SampleRequest
        {
            SampleRequestId = trial.SampleRequestId,
            ProductId = selectedFormula.ProductId,
            Status = SampleRequestStatus.SampleSent.ToString(),
            UpdatedDate = now.AddHours(-1),
            UpdatedBy = Guid.NewGuid()
        };

        SampleRequestSampleTrialApprovalRules.ApplyApproved(
            sampleRequest,
            trial,
            [selectedFormula, otherFormula],
            employeeId,
            now,
            SampleRequestSampleTrialApprovalRules.ApprovedCustomerReplyStatus);

        Assert.Equal(SampleTrialStatus.Approved, trial.Status);
        Assert.Equal("APPROVED", trial.CustomerReplyStatus);
        Assert.Equal(now, trial.CustomerReplyDate);
        Assert.Equal(employeeId, trial.CustomerReplyByEmployeeId);
        Assert.Equal(FormulaStatus.Completed.ToString(), selectedFormula.Status);
        Assert.True(selectedFormula.IsSelect);
        Assert.False(otherFormula.IsSelect);
        Assert.Equal(selectedFormula.FormulaId, sampleRequest.FormulaId);
        Assert.Equal(SampleRequestStatus.Completed.ToString(), sampleRequest.Status);
        Assert.Equal(now, sampleRequest.UpdatedDate);
        Assert.Equal(employeeId, sampleRequest.UpdatedBy);
    }

    private static Formula CreateFormula()
        => new()
        {
            FormulaId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Status = FormulaStatus.SampleSent.ToString(),
            IsActive = true
        };

    private static SampleRequestSampleTrial CreatePendingTrial(Formula formula)
        => new()
        {
            SampleRequestSampleTrialId = Guid.NewGuid(),
            SampleRequestId = Guid.NewGuid(),
            FormulaId = formula.FormulaId,
            Formula = formula,
            TrialNo = 1,
            Status = SampleTrialStatus.WaitingCustomerFeedback,
            IsActive = true
        };
}
