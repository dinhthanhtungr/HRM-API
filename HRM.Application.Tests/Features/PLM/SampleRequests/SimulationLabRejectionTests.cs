using HRM.Application.Features.Development.SampleWorkflowSimulation;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SimulationLabRejectionTests
{
    private static SimulationView Read(string id, string role, SimulationState? state = null) => SimulationTransitions.Evaluate(new() { ScenarioId = id, Audience = role, State = state });
    private static SimulationView Act(SimulationView view, string role, string action, string? reason = null) => SimulationTransitions.Evaluate(new() { ScenarioId = view.ScenarioId, Audience = role, State = view.State, ActionId = action, Input = new() { Note = reason } });
    [Theory]
    [InlineData("quote-new")]
    [InlineData("chip-new")]
    [InlineData("sample-new")]
    [InlineData("both-new")]
    [InlineData("sample-sent")]
    [InlineData("both-sample-sent")]
    public void LabRejectionDeclinesRequestAndPreservesFormulaAndDeliveryHistory(string id)
    {
        var before = Read(id, "Lab");
        Assert.Contains(before.Actions, a => a.Id == "RejectFormula" && a.Group == "LabOther");
        Assert.Throws<ArgumentException>(() => Act(before, "Sale", "RejectFormula", "Not authorized"));
        Assert.Throws<ArgumentException>(() => Act(before, "Lab", "RejectFormula", "  "));
        var rejected = Act(before, "Lab", "RejectFormula", "  Missing technical information  ");
        Assert.Equal("LabDeclined", rejected.State.Status);
        Assert.Equal("Missing technical information", rejected.State.RejectionReason);
        Assert.Empty(rejected.Actions); Assert.Empty(rejected.PendingLab);
        var notification = Assert.Single(rejected.State.Notifications);
        Assert.Equal("General", notification.Group); Assert.Equal("Lab", notification.Actor);
        Assert.Contains("Lab", notification.Title.Vi); Assert.Equal(rejected.State.RejectionReason, notification.Reason);
        var sale = Read(id, "Sale", rejected.State);
        Assert.DoesNotContain(sale.Actions, a => a.Id == "ResumeAfterLabRejection");
        Assert.Throws<ArgumentException>(() => Act(sale, "Sale", "ResumeAfterLabRejection", "Cannot reopen cancellation as pause"));
        Assert.Equal(before.State.Workflow!.FormulaStatus, rejected.State.Workflow!.FormulaStatus);
        Assert.Equal(before.State.Workflow.Plan, rejected.State.Workflow.Plan);
        Assert.Equal(before.State.Workflow.TrialNo, rejected.State.Workflow.TrialNo);
        Assert.Equal(before.State.PriceAccepted, rejected.State.PriceAccepted);
        Assert.Single(rejected.State.History);

    }
    [Theory]
    [InlineData("quote-completed")]
    [InlineData("quote-cancelled")]
    public void ClosedRequestsCannotBeRejectedByLab(string id) => Assert.DoesNotContain(Read(id, "Lab").Actions, a => a.Id == "RejectFormula");
}
