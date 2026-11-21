using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestWorkflowTests
{
    [Theory]
    [InlineData("InProgress", false)]
    [InlineData("New", false)]
    [InlineData("WaitingCustomerPriceConfirmation", true)]
    public void WaitingPriceStatusCanStillBeIdentifiedForTracking(string status, bool blocked)
    {
        var request = new SampleRequest { Status = status, InfoType = "Thẻ màu (3)" };
        Assert.Equal(blocked, SampleRequestCustomerPriceRules.IsBlocked(request));
        request.Status = "InProgress";
        Assert.False(SampleRequestCustomerPriceRules.IsBlocked(request));
    }

    [Fact]
    public void LabTodayIncludesWaitingRequestsEvenAfterFormulaConfirmation()
    {
        SampleRequestSampleTrialReportRow Row(string status, bool delay = false) => new()
        {
            SampleRequest = new SampleRequest { InfoType = "Thẻ màu (1)",
                Status = status, CreatedDate = new DateTime(2026, 10, 1), IsDelayed = delay }
        };
        var legacy = Row("InProgress");
        var blocked = Row("WaitingCustomerPriceConfirmation");
        var confirmed = Row("InProgress");
        var pinned = Row("WaitingCustomerPriceConfirmation", delay: true);
        var result = SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { legacy, blocked, confirmed, pinned }.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 10, 31) }).ToList();
        Assert.Equal(new[] { legacy, blocked, confirmed, pinned }, result);
        blocked.LabFormulaConfirmedAt = new DateTime(2026, 10, 31);
        var afterConfirmation = SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { blocked, pinned }.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 10, 31) }).ToList();
        Assert.Equal(new[] { blocked, pinned }, afterConfirmation);
    }

    [Fact]
    public void NewColourRequestCanSendWithoutApprovedPriceOrCustomerAcceptance()
    {
        var item = new SampleRequestSampleTrialReportDto { InfoType = "Thẻ màu (1)",
            SampleRequestStatus = "WaitingCustomerPriceConfirmation", RequiresCustomerPriceConfirmation = true,
            LabReceivedDate = new DateTime(2026, 10, 31, 8, 0, 0) };
        SampleRequestDailyWorkRules.Apply(item, new DateOnly(2026, 10, 31), false, true, false, true, null);
        Assert.Equal("AwaitingFormulaApproval", item.WorkStatus);
        Assert.Equal("Lab", item.ResponsibleRole);
        item.LabFormulaConfirmedAt = new DateTime(2026, 10, 31, 9, 0, 0);
        SampleRequestDailyWorkRules.Apply(item, new DateOnly(2026, 10, 31), false, true, false, true, null);
        Assert.Equal("AwaitingLabSend", item.WorkStatus);
        SampleRequestDailyWorkRules.Apply(item, new DateOnly(2026, 10, 31), true, true, false, true, null);
        Assert.Equal("AwaitingLabSend", item.WorkStatus);
        item.RealPriceQuoteDate = new DateTime(2026, 10, 31, 10, 0, 0);
        SampleRequestDailyWorkRules.Apply(item, new DateOnly(2026, 10, 31), true, true, false, true, null);
        Assert.Equal("AwaitingLabSend", item.WorkStatus);
        Assert.Equal("Lab", item.ResponsibleRole);
    }

    [Fact]
    public void NewPermissionsDoNotGiveLabUsersPriceAcceptanceOrStandardReceipt()
    {
        Assert.Contains(ApplicationRoles.Sales.SaleUser, ApplicationPermissionRoleSets.GetRoles(ApplicationPermissions.PLM.ConfirmCustomerPrice));
        Assert.DoesNotContain(ApplicationRoles.Lab.LabUser, ApplicationPermissionRoleSets.GetRoles(ApplicationPermissions.PLM.ConfirmCustomerPrice));
        Assert.Contains(ApplicationRoles.Lab.LabAdmin, ApplicationPermissionRoleSets.GetRoles(ApplicationPermissions.PLM.ReceiveLabStandard));
        Assert.DoesNotContain(ApplicationRoles.Sales.SaleUser, ApplicationPermissionRoleSets.GetRoles(ApplicationPermissions.PLM.ReceiveLabStandard));
        Assert.Contains(ApplicationRoles.Lab.LabUser, ApplicationPermissionRoleSets.GetRoles(ApplicationPermissions.PLM.ReceiveLabStandard));
    }

    [Fact]
    public void DailyAndFollowUpQueriesTranslateToPostgresWithoutConnecting()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var rows = from record in db.SampleRequests
                   join trial in db.SampleRequestSampleTrials on record.SampleRequestId equals trial.SampleRequestId into trials
                   from trial in trials.DefaultIfEmpty()
                   select new SampleRequestSampleTrialReportRow { SampleRequest = record, Trial = trial };
        var enriched = SampleRequestLabWorkQuery.Enrich(rows, db);
        var query = SampleRequestSampleTrialReportQueryRules.ApplyReportType(enriched,
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 10, 31) });
        Assert.DoesNotContain("WaitingCustomerPriceConfirmation", query.ToQueryString());
        Assert.DoesNotContain("SampleWorkflowVersion", query.ToQueryString());
        Assert.DoesNotContain("CustomerPriceConfirmedAt", query.ToQueryString());
        var eventQuery = SampleRequestCustomerPriceAudit.Events(db, Guid.NewGuid(), Guid.NewGuid())
            .GroupBy(x => x.SampleRequestId).Select(g => g.OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).First());
        Assert.Contains("SampleRequestWorkflowEvents", eventQuery.ToQueryString());
        Assert.DoesNotContain("AuditLogs", eventQuery.ToQueryString());
        Assert.Contains("SourceFormulaId", SampleRequestSaleWorkQuery.RequirePublishedQuotationPrice(enriched, db, "VND").ToQueryString());
        Assert.Contains("min(", SampleRequestSaleFollowUpQuery.Build(db, Guid.NewGuid(), Guid.NewGuid())
            .GroupBy(x => x.SampleRequestId).Select(x => new { Id = x.Key, Date = x.Min(y => y.DueDate) }).ToQueryString().ToLowerInvariant());
        Assert.True(db.Model.FindEntityType(typeof(SampleRequest))!.FindProperty(nameof(SampleRequest.UpdatedDate))!.IsConcurrencyToken);
        var tasks = SampleRequestSaleFollowUpQuery.Build(db, Guid.NewGuid(), Guid.NewGuid());
        var end = new DateTime(2026, 11, 1);
        Assert.Contains("DueDate", rows.Where(x => tasks.Any(t => t.SampleRequestId == x.SampleRequest.SampleRequestId && t.DueDate < end)).ToQueryString());
    }
}
