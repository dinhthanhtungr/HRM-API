using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkDecisionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DelegatedWriteMetadataSurvivesDailyReassignmentWithoutRegrant(bool allowed)
    {
        var actor = Guid.NewGuid(); var owner = Guid.NewGuid();
        var user = new User("SaleAdmin", actor);
        var item = new SampleRequestSampleTrialReportDto {
            SampleRequestStatus = "InProgress", ManagerSalesEmployeeId = owner,
            CanActForSaleOwner = allowed, WorkDecision = new("AwaitingPriceFeedback", "PriceFeedback", "Sale") };
        SampleRequestDailyWorkRules.Apply(item, new DateOnly(2026, 11, 4), false, false, false, true, actor);
        SampleWorkAssignment.Apply(item, user, new CurrentUserPermissionService(user), SampleRequestWorkAudience.Sale);
        Assert.Equal(allowed ? "PriceFeedback" : null, item.PrimaryActionCode);
        Assert.Equal(allowed, item.IsActionForCurrentUser);
        item.ManagerSalesEmployeeId = actor;
        item.CanActForSaleOwner = false;
        SampleWorkAssignment.Apply(item, user, new CurrentUserPermissionService(user), SampleRequestWorkAudience.Sale);
        Assert.Null(item.PrimaryActionCode);
    }
    private static readonly SampleWorkFacts Preparing = new() { Open = true, FormulaStatus = "Draft" };

    public static IEnumerable<object[]> Cases()
    {
        yield return ["chip prepare", Preparing, "ApproveFormula", "Lab", "PrepareChip"];
        yield return ["chip send", Preparing with { FormulaStatus = "Approved" }, "SendSample", "Lab", "PrepareChip"];
        yield return ["chip feedback", Preparing with { HasTrial = true, PendingTrial = true, Chip = true }, "FollowUpCustomer", "Sale", "AwaitingChipFeedback"];
        yield return ["chip rework", Preparing with { HasTrial = true, Chip = true, Failed = true, FormulaStatus = "Rework" }, "ReworkFormula", "Lab", "ReworkChip"];
        yield return ["chip finalize", Preparing with { HasTrial = true, Chip = true, Approved = true, FormulaStatus = "Rework" }, "FinalizeChip", "Lab", "FinalizeChipFormula"];
        yield return ["chip sale choice", Preparing with { HasTrial = true, Chip = true, Approved = true, FinalizedChip = true, FormulaStatus = "Completed" }, "CompleteRequest", "Sale", "AwaitingSaleDecision"];
        yield return ["kg prepare", Preparing with { RequestedKg = true }, "ApproveFormula", "Lab", "PrepareSample"];
        yield return ["kg awaits approved price", Preparing with { RequestedKg = true, FormulaStatus = "Approved", LabConfirmed = true }, "ApproveStandardPrice", "President", "AwaitingStandardPrice"];
        yield return ["kg quote", Preparing with { RequestedKg = true, FormulaStatus = "Approved", ApprovedPrice = true }, "SendQuotation", "Sale", "AwaitingPriceFeedback"];
        yield return ["kg price feedback", Preparing with { RequestedKg = true, FormulaStatus = "Approved", ApprovedPrice = true, Quoted = true }, "PriceFeedback", "Sale", "AwaitingPriceFeedback"];
        yield return ["kg send", Preparing with { RequestedKg = true, FormulaStatus = "Approved", AcceptedPrice = true }, "SendSample", "Lab", "ReadyToSendSample"];
        yield return ["kg sent", Preparing with { HasTrial = true, Kg = true, PendingTrial = true }, "FollowUpCustomer", "Sale", "AwaitingSampleFeedback"];
        yield return ["kg failed", Preparing with { HasTrial = true, Kg = true, Failed = true }, "CreateNewRequest", "Sale", "NewRequestRequired"];
        yield return ["unknown sent", Preparing with { HasTrial = true, UnknownSent = true }, "ReviewSample", "LabAdmin", "NeedsReview"];
        yield return ["legacy prepare", Preparing with { Legacy = true }, "ApproveFormula", "Lab", "LegacyPreparation"];
        yield return ["legacy send", Preparing with { Legacy = true, FormulaStatus = "Approved" }, "SendSample", "Lab", "LegacyPreparation"];
        yield return ["legacy complete", Preparing with { Legacy = true, Sent = true, FormulaStatus = "SampleSent" }, "CompleteRequest", "Sale", "LegacySent"];
        yield return ["legacy missing formula", Preparing with { Legacy = true, Sent = true, FormulaStatus = null }, "ReviewSample", "LabAdmin", "NeedsReview"];
        yield return ["legacy unsent formula", Preparing with { Legacy = true, Sent = true, FormulaStatus = "Approved" }, "ReviewSample", "LabAdmin", "NeedsReview"];
        yield return ["quote prepare", Preparing with { Quotation = true }, "ApproveFormula", "Lab", "QuotationPreparation"];
        yield return ["quote price", Preparing with { Quotation = true, LabConfirmed = true }, "ApproveStandardPrice", "President", "AwaitingStandardPrice"];
        yield return ["quote send", Preparing with { Quotation = true, LabConfirmed = true, ApprovedPrice = true }, "SendQuotation", "Sale", "QuotationPreparation"];
        yield return ["quote reply", Preparing with { Quotation = true, LabConfirmed = true, ApprovedPrice = true, Quoted = true }, "PriceFeedback", "Sale", "AwaitingPriceFeedback"];
        yield return ["quote complete", Preparing with { Quotation = true, LabConfirmed = true, AcceptedPrice = true, Quoted = true, FormulaStatus = "Completed" }, "CompleteRequest", "Sale", "QuotationAccepted"];
        yield return ["shared kg VU chip pending", Preparing with { HasSentKg = true, HasTrial = true, PendingTrial = true, Chip = true }, "ReviewSample", "LabAdmin", "NeedsReview"];
        yield return ["shared kg VU chip accepted", Preparing with { HasSentKg = true, HasTrial = true, Approved = true, Chip = true }, "ReviewSample", "LabAdmin", "NeedsReview"];
        yield return ["own kg pending remains feedback", Preparing with { HasSentKg = true, HasTrial = true, PendingTrial = true, Kg = true }, "FollowUpCustomer", "Sale", "AwaitingSampleFeedback"];
        yield return ["quotation locked VU needs review", Preparing with { Quotation = true, FormulaStatus = "Completed" }, "ReviewSample", "LabAdmin", "NeedsReview"];
        yield return ["quotation confirmed kg VU pricing preserved", Preparing with { Quotation = true, FormulaStatus = "Completed", HasSentKg = true, LabConfirmed = true }, "ApproveStandardPrice", "President", "AwaitingStandardPrice"];
        yield return ["quote Lab finalize", Preparing with { Quotation = true, LabConfirmed = true, AcceptedPrice = true, Quoted = true, FormulaStatus = "Approved" }, "FinalizeQuotation", "Lab", "FinalizeQuotationFormula"];
        yield return ["quote accepted invalid VU", Preparing with { Quotation = true, LabConfirmed = true, AcceptedPrice = true, Quoted = true, FormulaStatus = "SampleSent" }, "ReviewSample", "LabAdmin", "NeedsReview"];
        yield return ["closed", Preparing with { Open = false }, "", "", "Closed"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryBranchUsesOneDecisionForQueueAndAllThreePersonas(string name, object input, string code, string owner, string stage)
    {
        var facts = (SampleWorkFacts)input;
        var decision = SampleWorkDecisionRules.Resolve(facts);
        var expected = new SampleWorkDecision(stage, code, owner);
        Assert.True(expected == decision, $"{name}: expected {expected}, actual {decision}");
        var employee = Guid.NewGuid();
        var row = new SampleRequestSampleTrialReportRow
        {
            SampleRequest = new SampleRequest { Status = facts.Open ? "InProgress" : "Completed", ManagerBy = employee },
            WorkDecision = decision, NeedsSchedule = true
        };
        foreach (var (role, audience) in new[] { ("LabUser", SampleRequestWorkAudience.Lab), ("SaleUser", SampleRequestWorkAudience.Sale), ("LabAdmin", SampleRequestWorkAudience.LabAdmin) })
        {
            var user = new User(role, employee);
            var item = new SampleRequestSampleTrialReportDto { SampleRequestStatus = row.SampleRequest.Status,
                ManagerSalesEmployeeId = employee, WorkDecision = decision, NeedsLabSchedule = facts.Open && owner is "Lab" or "LabAdmin" };
            SampleWorkAssignment.Apply(item, user, new CurrentUserPermissionService(user), audience);
            var primary = !facts.Open ? null : audience == SampleRequestWorkAudience.LabAdmin
                ? owner is "Lab" or "LabAdmin" ? "ScheduleLabWork" : null
                : audience == SampleRequestWorkAudience.Lab && owner == "Lab" || audience == SampleRequestWorkAudience.Sale && owner == "Sale" ? code : null;
            Assert.Equal(primary, item.PrimaryActionCode);
            Assert.Equal(primary != null, item.IsActionForCurrentUser);
            Assert.Equal(code, item.NextActionCode);
            Assert.Equal(owner, item.ResponsibleRole);
            var query = new GetSampleRequestSampleTrialsQuery { WorkAudience = audience, WorkBucket = SampleRequestWorkBucket.ActionRequired };
            var selected = SampleRequestWorkQueueQuery.Apply(new[] { row }.AsQueryable(), query, null!).Any();
            Assert.Equal(primary != null, selected);
        }
    }

    [Fact]
    public void OtherSaleCannotActAndCompletedSchedulingDisappearsWithoutChangingTechnicalWork()
    {
        var user = new User("SaleUser", Guid.NewGuid());
        var item = new SampleRequestSampleTrialReportDto { SampleRequestStatus = "InProgress", ManagerSalesEmployeeId = Guid.NewGuid(),
            WorkDecision = new("AwaitingPriceFeedback", "PriceFeedback", "Sale") };
        SampleWorkAssignment.Apply(item, user, new CurrentUserPermissionService(user), SampleRequestWorkAudience.Sale);
        Assert.Null(item.PrimaryActionCode);
        var admin = new User("LabAdmin", user.EmployeeId!.Value);
        item.WorkDecision = new("PrepareChip", "ApproveFormula", "Lab");
        item.NeedsLabSchedule = false;
        SampleWorkAssignment.Apply(item, admin, new CurrentUserPermissionService(admin), SampleRequestWorkAudience.LabAdmin);
        Assert.Null(item.PrimaryActionCode);
        Assert.Equal("ApproveFormula", item.NextActionCode);
        Assert.Contains("EditLabSchedule", item.SupplementaryActionCodes);
        Assert.False(new CurrentUserPermissionService(admin).HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow));
    }

    private sealed record User(string Role, Guid Employee) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.NewGuid();
        public Guid? EmployeeId => Employee;
        public Guid? CompanyId => Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [Role];
        public bool IsInRole(string role) => role == Role;
    }
}
