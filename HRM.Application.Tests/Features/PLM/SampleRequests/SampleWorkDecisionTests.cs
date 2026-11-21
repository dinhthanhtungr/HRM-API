using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkDecisionTests
{
    [Theory]
    [InlineData("RequestLabFinalization")]
    [InlineData("RequestChipFinalization")]
    public void AllowedFinalizationRequestDoesNotAssignLabOrRequireSaleToRequestIt(string code)
    {
        var employee = Guid.NewGuid(); var user = new User("SaleUser", employee);
        var item = new SampleRequestSampleTrialReportDto {
            SampleRequestStatus = "InProgress", ManagerSalesEmployeeId = employee,
            HasCurrentDeliveryFeedback = true, CanUpdateCustomerFeedback = true,
            WorkDecision = new("AwaitingSaleAfterChip", "ChooseAfterChip", "Sale"),
            Flow = new("ChipAndSample", "AwaitingSaleAfterChip", null, null, null, 0, [code]) };
        SampleWorkAssignment.Apply(item, user, new CurrentUserPermissionService(user), SampleRequestWorkAudience.Sale);
        Assert.Equal("ChooseAfterChip", item.PrimaryActionCode);
        Assert.Empty(item.PendingActionCodes);
        Assert.Equal("Sale", item.ResponsibleRole);
        Assert.Contains(code, item.Flow!.AllowedActions);
        Assert.True(item.IsActionForCurrentUser);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecordedDeliveryFeedbackDoesNotBecomeUnfinishedWork(bool recorded)
    {
        var employee = Guid.NewGuid();
        var user = new User("SaleUser", employee);
        var item = new SampleRequestSampleTrialReportDto {
            SampleRequestStatus = "InProgress", ManagerSalesEmployeeId = employee,
            CanUpdateCustomerFeedback = true, HasCurrentDeliveryFeedback = recorded,
            WorkDecision = new("AwaitingSampleFeedback", "FollowUpCustomer", "Sale") };
        SampleWorkAssignment.Apply(item, user, new CurrentUserPermissionService(user), SampleRequestWorkAudience.Sale);
        Assert.Equal(!recorded, item.PendingActionCodes.Contains("FollowUpCustomer"));
        Assert.Equal(recorded, item.SupplementaryActionCodes.Contains("ViewCustomerFeedback"));
        Assert.True(item.CanUpdateCustomerFeedback);
    }
    [Fact]
    public void SaleAssignmentPrioritizesMissingPriceAndNeverListsNavigationAsWork()
    {
        var employee = Guid.NewGuid();
        var user = new User("SaleUser", employee);
        var item = new SampleRequestSampleTrialReportDto { SampleRequestStatus = "InProgress",
            ManagerSalesEmployeeId = employee, CanUpdateCustomerFeedback = true, CanConfirmCustomerPrice = true,
            RealPriceQuoteDate = new DateTime(2026, 11, 15), WorkDecision = new("AwaitingSaleAfterChip", "ChooseAfterChip", "Sale") };
        SampleWorkAssignment.Apply(item, user, new CurrentUserPermissionService(user), SampleRequestWorkAudience.Sale);
        Assert.Equal("PriceFeedback", item.PrimaryActionCode);
        Assert.Equal(new[] { "PriceFeedback", "FollowUpCustomer" }, item.PendingActionCodes);
        Assert.DoesNotContain("ChooseAfterChip", item.PendingActionCodes);
    }
    [Fact]
    public void CompletionChangesTranslateToSqlWithoutOpeningADatabaseConnection()
    {
        using var connection = new NpgsqlConnection();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var source = from request in db.SampleRequests.Where(x => x.IsActive)
                     join trial in db.SampleRequestSampleTrials.Where(x => x.IsActive) on request.SampleRequestId equals trial.SampleRequestId into trials
                     from trial in trials.DefaultIfEmpty()
                     select new SampleRequestSampleTrialReportRow { SampleRequest = request, Trial = trial, ConfirmedFormulaId = request.FormulaId,
                         LabFormulaConfirmedAt = request.LabReceivedDate, HasAcceptedCustomerPrice = false, HasFinalizedChip = false, HasPriceFeedback = false, TrialCount = 0 };
        var query = SampleWorkDecisionQuery.Enrich(source, db, db, "VND");
        var sql = query.Where(x => x.WorkDecision!.Code == "TrackRequest").Take(15)
            .Select(x => x.SampleRequest.SampleRequestId).ToQueryString();
        Assert.Contains("RequestCompletionDeferred", sql);
        Assert.Contains("ColorChipRequested", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }
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
        yield return ["chip approved choice", Preparing with { RequiresPriceAcceptance = false, HasTrial = true, Chip = true, Approved = true, FormulaStatus = "Rework" }, "ChooseAfterChip", "Sale", "AwaitingSaleAfterChip"];
        yield return ["accepted chip stays sent", Preparing with { RequiresPriceAcceptance = false, HasTrial = true, Chip = true, Approved = true, FormulaStatus = "SampleSent" }, "ChooseAfterChip", "Sale", "AwaitingSaleAfterChip"];
        yield return ["sent chip explicitly requested to finalize", Preparing with { RequiresPriceAcceptance = false, HasTrial = true, Chip = true, Approved = true, ChipFinalizationRequested = true, FormulaStatus = "SampleSent" }, "FinalizeChip", "Lab", "FinalizeChipFormula"];
        yield return ["sent accepted chip requested again", Preparing with { HasTrial = true, Chip = true, Approved = true, DeliveryRequested = true, FormulaStatus = "SampleSent" }, "SendSample", "Lab", "PrepareChip"];
        yield return ["sent accepted chip continues kg", Preparing with { HasTrial = true, Chip = true, Approved = true, RequestedKg = true, FormulaStatus = "SampleSent" }, "SendSample", "Lab", "ReadyToSendSample"];
        yield return ["chip follow up", Preparing with { RequiresPriceAcceptance = false, HasTrial = true, Chip = true, Approved = true, ChipDecisionDeferred = true, FormulaStatus = "Rework" }, "TrackApprovedChip", "Sale", "ChipFollowUp"];
        yield return ["withdraw completion", Preparing with { HasTrial = true, Chip = true, Approved = true, CompletionDeferred = true, ChipFinalizationRequested = true, FormulaStatus = "Rework" }, "TrackRequest", "Sale", "RequestFollowUp"];
        yield return ["request chips after completion", Preparing with { HasTrial = true, Chip = true, Approved = true, ChipDeliveryRequested = true, FormulaStatus = "Approved", FinalizedChip = true }, "SendSample", "Lab", "PrepareChip"];
        yield return ["chip finalize", Preparing with { RequiresPriceAcceptance = false, HasTrial = true, Chip = true, Approved = true, ChipFinalizationRequested = true, FormulaStatus = "Rework" }, "FinalizeChip", "Lab", "FinalizeChipFormula"];
        yield return ["chip sale choice", Preparing with { RequiresPriceAcceptance = false, HasTrial = true, Chip = true, Approved = true, FinalizedChip = true, FormulaStatus = "Completed" }, "CompleteRequest", "Sale", "AwaitingSaleDecision"];
        yield return ["revised VU after rejection", Preparing with { Quotation = true, RevisedFormulaNeedsPrice = true, ApprovedPrice = true, FormulaStatus = "Approved", Quoted = true, LabConfirmed = true }, "ApproveStandardPrice", "President", "AwaitingStandardPrice"];
        yield return ["price rejection choice", Preparing with { Quotation = true, PriceDecisionRequired = true }, "ChooseRejectedPriceAction", "Sale", "AwaitingPriceDecision"];
        yield return ["price rejection waiting", Preparing with { Quotation = true, PriceDecisionRequired = true, PriceDecisionDeferred = true }, "TrackRejectedPrice", "Sale", "PriceFollowUp"];
        yield return ["price rejected", Preparing with { Quotation = true, PriceAdjustmentRequired = true, FormulaStatus = "Approved", Quoted = true }, "AdjustFormulaPrice", "Lab", "PriceRework"];
        yield return ["kg price rejected", Preparing with { RequestedKg = true, PriceAdjustmentRequired = true, FormulaStatus = "Approved", Quoted = true }, "AdjustFormulaPrice", "Lab", "PriceRework"];
        yield return ["kg prepare", Preparing with { RequestedKg = true }, "ApproveFormula", "Lab", "PrepareSample"];
        yield return ["kg awaits approved price", Preparing with { RequestedKg = true, FormulaStatus = "Approved", LabConfirmed = true }, "SendSample", "Lab", "ReadyToSendSample"];
        yield return ["kg quote", Preparing with { RequestedKg = true, FormulaStatus = "Approved", ApprovedPrice = true }, "SendSample", "Lab", "ReadyToSendSample"];
        yield return ["kg price feedback", Preparing with { RequestedKg = true, FormulaStatus = "Approved", ApprovedPrice = true, Quoted = true }, "SendSample", "Lab", "ReadyToSendSample"];
        yield return ["kg send", Preparing with { RequestedKg = true, FormulaStatus = "Approved", AcceptedPrice = true }, "SendSample", "Lab", "ReadyToSendSample"];
        yield return ["kg sent", Preparing with { HasTrial = true, Kg = true, PendingTrial = true }, "FollowUpCustomer", "Sale", "AwaitingSampleFeedback"];
        yield return ["kg accepted awaiting standard price", Preparing with { HasTrial = true, Kg = true, Approved = true }, "ApproveStandardPrice", "President", "AwaitingStandardPrice"];
        yield return ["kg accepted awaiting quote", Preparing with { HasTrial = true, Kg = true, Approved = true, ApprovedPrice = true }, "SendQuotation", "Sale", "AwaitingPriceFeedback"];
        yield return ["kg accepted awaiting price feedback", Preparing with { HasTrial = true, Kg = true, Approved = true, ApprovedPrice = true, Quoted = true }, "PriceFeedback", "Sale", "AwaitingPriceFeedback"];
        yield return ["kg both accepted", Preparing with { HasTrial = true, Kg = true, Approved = true, AcceptedPrice = true }, "CompleteRequest", "Sale", "AwaitingSaleDecision"];
        yield return ["kg failed", Preparing with { HasTrial = true, Kg = true, Failed = true }, "ReworkFormula", "Lab", "ReworkSample"];
        yield return ["kg re-confirmed", Preparing with { RequestedKg = true, HasTrial = true, Kg = true, Failed = true, FormulaStatus = "Approved" }, "SendSample", "Lab", "ReadyToSendSample"];
        yield return ["unknown sent", Preparing with { HasTrial = true, UnknownSent = true }, "ReviewSample", "LabAdmin", "NeedsReview"];
        yield return ["legacy prepare", Preparing with { Legacy = true }, "ApproveFormula", "Lab", "LegacyPreparation"];
        yield return ["legacy send", Preparing with { Legacy = true, FormulaStatus = "Approved" }, "SendSample", "Lab", "LegacyPreparation"];
        yield return ["legacy complete", Preparing with { Legacy = true, Sent = true, FormulaStatus = "SampleSent" }, "CompleteRequest", "Sale", "LegacySent"];
        yield return ["legacy missing formula", Preparing with { Legacy = true, Sent = true, FormulaStatus = null }, "ReviewSample", "LabAdmin", "NeedsReview"];
        yield return ["legacy unsent formula", Preparing with { Legacy = true, Sent = true, FormulaStatus = "Approved" }, "ReviewSample", "LabAdmin", "NeedsReview"];
        yield return ["quote prepare", Preparing with { Quotation = true }, "ApproveFormula", "Lab", "QuotationPreparation"];
        yield return ["quote price", Preparing with { Quotation = true, FormulaStatus = "Approved", LabConfirmed = true }, "ApproveStandardPrice", "President", "AwaitingStandardPrice"];
        yield return ["quote send", Preparing with { Quotation = true, FormulaStatus = "Approved", LabConfirmed = true, ApprovedPrice = true }, "SendQuotation", "Sale", "QuotationPreparation"];
        yield return ["quote reply", Preparing with { Quotation = true, FormulaStatus = "Approved", LabConfirmed = true, ApprovedPrice = true, Quoted = true }, "PriceFeedback", "Sale", "AwaitingPriceFeedback"];
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
        var progressQuery = new GetSampleRequestSampleTrialsQuery
        {
            WorkAudience = SampleRequestWorkAudience.LabAdmin,
            WorkBucket = SampleRequestWorkBucket.LabProgress
        };
        Assert.Equal(facts.Open && owner == "Lab",
            SampleRequestWorkQueueQuery.Apply(new[] { row }.AsQueryable(), progressQuery, null!).Any());
        foreach (var (role, audience) in new[] { ("LabUser", SampleRequestWorkAudience.Lab), ("SaleUser", SampleRequestWorkAudience.Sale), ("LabAdmin", SampleRequestWorkAudience.LabAdmin) })
        {
            var user = new User(role, employee);
            var item = new SampleRequestSampleTrialReportDto { SampleRequestStatus = row.SampleRequest.Status,
                ManagerSalesEmployeeId = employee, WorkDecision = decision, NeedsLabSchedule = facts.Open && owner is "Lab" or "LabAdmin" };
            SampleWorkAssignment.Apply(item, user, new CurrentUserPermissionService(user), audience);
            var primary = !facts.Open ? null : audience == SampleRequestWorkAudience.LabAdmin
                ? owner is "Lab" or "LabAdmin" ? code : "ScheduleLabWork"
                : audience == SampleRequestWorkAudience.Lab && owner == "Lab" || audience == SampleRequestWorkAudience.Sale && owner == "Sale" ? code : null;
            Assert.Equal(primary, item.PrimaryActionCode);
            Assert.Equal(primary != null, item.IsActionForCurrentUser);
            Assert.Equal(code, item.NextActionCode);
            Assert.Equal(owner, item.ResponsibleRole);
            var query = new GetSampleRequestSampleTrialsQuery { WorkAudience = audience, WorkBucket = SampleRequestWorkBucket.ActionRequired };
            var selected = SampleRequestWorkQueueQuery.Apply(new[] { row }.AsQueryable(), query, null!).Any();
            Assert.Equal(audience == SampleRequestWorkAudience.LabAdmin
                ? facts.Open && owner is "Lab" or "LabAdmin" : primary != null, selected);
        }
    }

    [Fact]
    public void OtherSaleCannotActAndExistingScheduleCanBeUpdatedWithoutChangingTechnicalWork()
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
        Assert.Equal("ApproveFormula", item.PrimaryActionCode);
        Assert.Equal("ApproveFormula", item.NextActionCode);
        Assert.Contains("EditLabSchedule", item.SupplementaryActionCodes);
        Assert.True(new CurrentUserPermissionService(admin).HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow));
    }

    [Theory]
    [InlineData("InProgress", "Sale", "PriceFeedback", "ScheduleLabWork")]
    [InlineData("InProgress", "President", "ApproveStandardPrice", "ScheduleLabWork")]
    [InlineData("InProgress", "LabAdmin", "ReviewSample", "ReviewSample")]
    [InlineData("Completed", "Sale", "PriceFeedback", null)]
    [InlineData("Cancelled", "Lab", "ApproveFormula", null)]
    public void ExistingScheduleRemainsEditableOnlyForOpenRequests(string status, string role, string action, string? expected)
    {
        var user = new User("LabAdmin", Guid.NewGuid());
        var item = new SampleRequestSampleTrialReportDto
        {
            SampleRequestStatus = status, NeedsLabSchedule = false,
            WorkDecision = new("stage", action, role)
        };
        SampleWorkAssignment.Apply(item, user, new CurrentUserPermissionService(user), SampleRequestWorkAudience.LabAdmin);
        Assert.Equal(expected, item.PrimaryActionCode);
        Assert.Equal(action, item.NextActionCode);
        Assert.Equal(expected is not null, item.IsActionForCurrentUser);
    }

    [Theory]
    [InlineData("LabUser", true, SampleRequestWorkAudience.Lab, null)]
    [InlineData("LabUser", true, SampleRequestWorkAudience.Sale, null)]
    [InlineData("LabAdmin", true, SampleRequestWorkAudience.Lab, null)]
    [InlineData("LabUser", true, SampleRequestWorkAudience.LabAdmin, "ReviewSample")]
    [InlineData("LabAdmin", true, SampleRequestWorkAudience.LabAdmin, "ReviewSample")]
    [InlineData("LabUser", true, null, "ReviewSample")]
    [InlineData("SaleUser", true, SampleRequestWorkAudience.LabAdmin, null)]
    [InlineData("LabAdmin", false, SampleRequestWorkAudience.LabAdmin, null)]
    [InlineData("UnknownRole", true, SampleRequestWorkAudience.LabAdmin, null)]
    public void ReviewAssignmentRespectsAudienceWithoutRevokingSchedulePermission(string role, bool authenticated,
        SampleRequestWorkAudience? audience, string? expected)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User(role, authenticated);
        var permissions = new CurrentUserPermissionService(user);
        var item = new SampleRequestSampleTrialReportDto { SampleRequestStatus = "InProgress",
            WorkDecision = new("NeedsReview", "ReviewSample", "LabAdmin") };
        SampleWorkAssignment.Apply(item, user, permissions, audience);
        Assert.Equal(expected, item.PrimaryActionCode);
        Assert.Equal(expected != null, item.IsActionForCurrentUser);
        Assert.Equal("ReviewSample", item.NextActionCode);
        Assert.Equal("LabAdmin", item.ResponsibleRole);
        var canSchedule = permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule);
        Assert.Equal(canSchedule, item.CanEditLabSchedule);
        Assert.Equal(canSchedule, item.SupplementaryActionCodes.Contains("EditLabSchedule"));
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
