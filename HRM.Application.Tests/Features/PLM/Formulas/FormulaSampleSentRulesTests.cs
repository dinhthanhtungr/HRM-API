using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaSampleSentRulesTests
{
    [Fact]
    public async Task AnotherFormulaCreatesNewTrialAndPreservesSentHistory()
    {
        var (db, store) = HRM.Application.Tests.Features.PLM.ProductionOrders.ProductionOrderTestStore
            .CreateContext<HRM.Application.Abstractions.Persistence.PLM.IPLMWriteDbContext>();
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), SampleQuantity = 2 };
        var oldDate = new DateTime(2026, 11, 2);
        var old = new SampleRequestSampleTrial { SampleRequestSampleTrialId = Guid.NewGuid(), SampleRequestId = request.SampleRequestId,
            TrialNo = 1, FormulaId = Guid.NewGuid(), BatchNo = "VU-OLD", Status = SampleTrialStatus.Failed,
            SentDate = oldDate, DeliveredSampleQuantityKg = 2, CustomerReplyNote = "Keep this feedback", IsActive = true };
        store.Seed(nameof(db.SampleRequestSampleTrials), old);
        var nextFormula = Guid.NewGuid();
        var service = new HRM.Application.Features.PLM.Formulas.Services.FormulaWriteService(db, null!);
        var next = await service.EnsureSampleSentTrialAsync(request, nextFormula, "VU-NEXT", request.CompanyId,
            Guid.NewGuid(), oldDate.AddDays(1), 3, default);
        Assert.Equal(2, next.TrialNo);
        Assert.Equal(request.SampleRequestId, next.SampleRequestId);
        Assert.Equal(nextFormula, next.FormulaId);
        Assert.Equal("VU-NEXT", next.BatchNo);
        Assert.NotEqual(old.SampleRequestSampleTrialId, next.SampleRequestSampleTrialId);
        Assert.Equal(oldDate, old.SentDate);
        Assert.Equal("VU-OLD", old.BatchNo);
        Assert.Equal(SampleTrialStatus.Failed, old.Status);
        Assert.Equal("Keep this feedback", old.CustomerReplyNote);
        Assert.Equal(2m, old.DeliveredSampleQuantityKg);
        Assert.Null(next.CustomerReplyNote);
    }
    [Fact]
    public async Task RealLegacyDeliveryQuantityUsesExistingDeliveryRecordWithActorAndDate()
    {
        var (db, store) = HRM.Application.Tests.Features.PLM.ProductionOrders.ProductionOrderTestStore
            .CreateContext<HRM.Application.Abstractions.Persistence.PLM.IPLMWriteDbContext>();
        store.Seed<SampleRequestSampleTrial>(nameof(db.SampleRequestSampleTrials));
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), ExternalId = "OLD-TP",
            CreatedDate = new DateTime(2026, 10, 31), SampleQuantity = 5, Status = "SampleSent" };
        var actor = Guid.NewGuid(); var formula = Guid.NewGuid(); var now = new DateTime(2026, 11, 9);
        var service = new HRM.Application.Features.PLM.Formulas.Services.FormulaWriteService(db, null!);
        var record = await service.EnsureSampleSentTrialAsync(request, formula, "VU-OLD", request.CompanyId, actor, now, 0.1m, default);
        Assert.Equal(0.1m, record.DeliveredSampleQuantityKg);
        Assert.Equal(formula, record.FormulaId);
        Assert.Equal(actor, record.SentByEmployeeId);
        Assert.Equal(now, record.SentDate);
        Assert.Contains(record, store.Added);
        Assert.Equal(5, request.SampleQuantity);
        Assert.Null(record.CustomerReplyDate);
    }
    [Theory]
    [InlineData(null, false)]
    [InlineData("0", false)]
    [InlineData("-0.1", false)]
    [InlineData("0.1", true)]
    [InlineData("99999999999999.9999", true)]
    [InlineData("100000000000000", false)]
    public void LegacyDeliveryRequiresPositiveQuantityWithinExistingColumnRange(string? value, bool valid)
        => Assert.Equal(valid, FormulaSampleSentRules.IsValidLegacyQuantity(value is null ? null : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    [Theory]
    [InlineData(SampleTrialStatus.Failed, true, true)]
    [InlineData(SampleTrialStatus.Approved, true, false)]
    [InlineData(SampleTrialStatus.WaitingCustomerFeedback, true, false)]
    [InlineData(SampleTrialStatus.Failed, false, false)]
    public void RetryKgRequiresFailedDeliveryOfSameRequestAndFormula(SampleTrialStatus status, bool sameRequest, bool expected)
    {
        var requestId = Guid.NewGuid(); var formulaId = Guid.NewGuid();
        var trial = new SampleRequestSampleTrial
        {
            IsActive = true, SampleRequestId = sameRequest ? requestId : Guid.NewGuid(),
            FormulaId = formulaId, Status = status, SentDate = DateTime.Today,
            DeliveredSampleQuantityKg = 3
        };
        Assert.Equal(expected, FormulaSampleSentRules.CanRetryFailedKg(trial, requestId, formulaId));
        Assert.False(FormulaSampleSentRules.CanRetryFailedKg(trial, requestId, Guid.NewGuid()));
    }
    [Theory]
    [InlineData("Completed", FormulaStatus.Approved, true)]
    [InlineData("Completed", FormulaStatus.SampleSent, false)]
    [InlineData("Cancelled", FormulaStatus.Approved, false)]
    [InlineData("Cancelled", FormulaStatus.SampleSent, false)]
    [InlineData("InProgress", FormulaStatus.Approved, true)]
    [InlineData("InProgress", FormulaStatus.SampleSent, true)]
    public void CanReceiveFormulaStatus_AllowsImprovementConfirmationOnCompletedRequestOnly(
        string requestStatus, FormulaStatus targetStatus, bool expected)
    {
        Assert.Equal(expected, FormulaSampleSentRules.CanReceiveFormulaStatus(requestStatus, targetStatus));
    }

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("Approved", true)]
    [InlineData("approved", true)]
    [InlineData("SampleSent", false)]
    [InlineData("Completed", false)]
    [InlineData("Cancelled", false)]
    [InlineData("Rejected", false)]
    [InlineData("PendingSaleConfirmation", false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public void CanConfirmFromStatus_DoesNotReopenDeliveredOrTerminalFormula(string? status, bool expected)
    {
        Assert.Equal(expected, FormulaSampleSentRules.CanConfirmFromStatus(status));
    }

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
        Assert.Equal(SampleTrialStatus.WaitingCustomerFeedback, trial.Status);
        Assert.Null(trial.RequestReceivedDate);
        Assert.Equal(100m, trial.DeliveredSampleQuantityKg);
        Assert.Equal(now, trial.SentDate);
        Assert.Equal(employeeId, trial.SentByEmployeeId);
        Assert.Equal("WAITING", trial.CustomerReplyStatus);
        Assert.Equal(employeeId, trial.UpdatedBy);
        Assert.Equal(now, trial.UpdatedDate);
    }
}
