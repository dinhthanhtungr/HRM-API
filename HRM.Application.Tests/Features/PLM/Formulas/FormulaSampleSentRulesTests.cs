using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaSampleSentRulesTests
{
    [Fact]
    public void ValidateRequest_RejectsNegativeQuantityWhenSendingSample()
    {
        var request = new UpdateFormulaStatusRequest
        {
            Status = FormulaStatus.SampleSent,
            SampleRequestId = Guid.NewGuid(),
            DeliveredSampleQuantityKg = -0.0001m
        };

        Assert.NotNull(FormulaSampleSentRules.ValidateRequest(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2.5)]
    public void ValidateRequest_AcceptsSampleRequestAndNonNegativeQuantity(double quantity)
    {
        var request = new UpdateFormulaStatusRequest
        {
            Status = FormulaStatus.SampleSent,
            SampleRequestId = Guid.NewGuid(),
            DeliveredSampleQuantityKg = (decimal)quantity
        };

        Assert.Null(FormulaSampleSentRules.ValidateRequest(request));
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("SampleSent")]
    public void CanSendFromStatus_AllowsApprovedAndRepeatedSampleSent(string status)
    {
        Assert.True(FormulaSampleSentRules.CanSendFromStatus(status));
    }

    [Fact]
    public void PrepareTrialForDelivery_SnapshotsFormulaAndStartsWaitingReply()
    {
        var formulaId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var now = new DateTime(2026, 8, 14, 9, 30, 0);
        var trial = new SampleRequestSampleTrial
        {
            SampleRequestSampleTrialId = Guid.NewGuid(),
            SampleRequestId = Guid.NewGuid(),
            TrialNo = 1,
            Status = SampleTrialStatus.Draft,
            CreatedDate = now.AddDays(-1)
        };

        FormulaSampleSentRules.PrepareTrialForDelivery(
            trial,
            formulaId,
            " VU260400089 ",
            employeeId,
            now,
            100m);

        Assert.Equal(formulaId, trial.FormulaId);
        Assert.Equal("VU260400089", trial.BatchNo);
        Assert.Equal(SampleTrialStatus.SampleSent, trial.Status);
        Assert.Equal(100m, trial.DeliveredSampleQuantityKg);
        Assert.Equal(now, trial.SentDate);
        Assert.Equal(employeeId, trial.SentByEmployeeId);
        Assert.Equal("WAITING", trial.CustomerReplyStatus);
        Assert.Equal(employeeId, trial.UpdatedBy);
        Assert.Equal(now, trial.UpdatedDate);
    }
}
