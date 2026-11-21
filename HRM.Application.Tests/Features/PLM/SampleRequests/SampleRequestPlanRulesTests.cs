using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestPlanRulesTests
{
    [Theory]
    [InlineData("RequestChip", "ColorChipRequested", 0, 0, true)]
    [InlineData("RequestSample", "TrialSampleRequested", 10, 10, true)]
    [InlineData("RequestSample", "TrialSampleRequested", 10, 11, false)]
    [InlineData("RequestChipThenSample", "ColorChipThenSampleRequested", 10, 10, true)]
    public void UnchangedPendingPlanDoesNotCreateAnotherInstruction(string action, string eventType,
        double savedKg, double newKg, bool unchanged)
    {
        var request = new SampleRequest { CreatedDate = new(2026, 11, 2), Status = "InProgress",
            InfoType = "Thẻ màu (2)", SampleQuantity = savedKg };
        var plan = new SampleRequestWorkflowEvent { EventType = eventType, RecordedAt = new(2026, 11, 3) };
        Assert.Equal(unchanged, SampleRequestPlanRules.IsUnchangedPendingPlan(request, [plan], [], action, newKg, 2));
        Assert.False(SampleRequestPlanRules.IsUnchangedPendingPlan(request, [plan], [], "RequestChip", 0, 3));
        request.Status = "Completed";
        Assert.False(SampleRequestPlanRules.IsUnchangedPendingPlan(request, [plan], [], action, newKg, 2));
    }

    [Fact]
    public void CombinedPlanIsPendingAfterChipButCanBeRequestedAgainAfterKgDelivery()
    {
        var request = new SampleRequest { CreatedDate = new(2026, 11, 2), Status = "InProgress",
            InfoType = "Thẻ màu (2)", SampleQuantity = 10 };
        var plan = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.ChipThenSampleRequested, RecordedAt = new(2026, 11, 3) };
        var chip = new SampleRequestSampleTrial { IsActive = true, CreatedDate = new(2026, 11, 4), SentDate = new(2026, 11, 4) };
        Assert.True(SampleRequestPlanRules.IsUnchangedPendingPlan(request, [plan], [chip], ChipThenSampleRules.Action, 10, 2));
        chip.DeliveredSampleQuantityKg = 10;
        Assert.False(SampleRequestPlanRules.IsUnchangedPendingPlan(request, [plan], [chip], ChipThenSampleRules.Action, 10, 2));
    }
    [Theory]
    [InlineData("RequestSample", 2, 0)]
    [InlineData("RequestChip", 0, 3)]
    [InlineData("RequestChipThenSample", 100, 2)]
    public void FollowUpKeepsIdentityButNeverCopiesTheOldFormulaOrLifecycle(string action, double kg, int sets)
    {
        var source = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(), ProductId = Guid.NewGuid(), FormulaId = Guid.NewGuid(),
            Status = "Completed", InfoType = "Báo giá", ExternalId = "TP_TEST" };
        var command = SampleRequestFollowUpCreator.Build(source, new ApplySampleFlowActionCommand {
            Action = action, RequestedKg = kg, RequestedSets = sets, Note = "Customer changed plan"
        });
        Assert.Equal(source.CustomerId, command.CustomerId);
        Assert.Equal(source.ProductId, command.ProductId);
        Assert.Equal(source.CompanyId, command.CompanyId);
        Assert.Null(command.FormulaId);
        Assert.Null(command.ExpectedPrice);
        Assert.Null(command.ExpectedDeliveryDate);
        Assert.Equal(kg, command.SampleQuantity);
        Assert.Contains(source.ExternalId, command.InitialLabMessage);
        Assert.True(command.UseExistingTransaction);
        Assert.Equal("Completed", source.Status);
        Assert.NotNull(source.FormulaId);
    }
    [Theory]
    [InlineData("InProgress", true)]
    [InlineData("Completed", true)]
    [InlineData("Cancelled", true)]
    public void SaleChoicesDoNotDependOnThePrimaryTask(string status, bool allowed)
    {
        var request = new SampleRequest { CreatedDate = new(2026, 11, 2), Status = status };
        var formula = new Formula { Status = "Completed" };
        var choices = SampleRequestPlanRules.Actions(request, null, formula, true, true);
        Assert.Equal(allowed, choices.Contains("RequestChip"));
        Assert.Equal(allowed, choices.Contains("RequestSample"));
        Assert.Equal(allowed, choices.Contains(ChipThenSampleRules.Action));
        Assert.Equal(allowed, choices.Contains("RequestRequote"));
        Assert.Empty(SampleRequestPlanRules.Actions(request, null, formula, false, true));
        request.CreatedDate = new(2026, 10, 31);
        Assert.Contains("RequestSample", SampleRequestPlanRules.Actions(request, null, formula, true, true));
    }

    [Fact]
    public void PendingDeliveryAllowsExplicitSampleRequestButNotOtherDeliveryPlans()
    {
        var request = new SampleRequest { CreatedDate = new(2026, 11, 2), Status = "InProgress" };
        var pending = new SampleRequestSampleTrial { Status = SampleTrialStatus.WaitingCustomerFeedback };
        var choices = SampleRequestPlanRules.Actions(request, pending, new Formula { Status = "Approved" }, true, true);
        Assert.DoesNotContain("RequestChip", choices);
        Assert.Contains("RequestSample", choices);
        Assert.DoesNotContain(ChipThenSampleRules.Action, choices);
        Assert.Contains("RequestRequote", choices);
    }

    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(true, 0, false)]
    [InlineData(false, 2, false)]
    public void KgHistoryKeepsTheSameRequest(bool everSentKg, int kg, bool expected)
        => Assert.Equal(expected, SampleRequestPlanRules.NeedsNewRequest(
            new SampleRequestSampleTrial { DeliveredSampleQuantityKg = kg }, everSentKg));
}
