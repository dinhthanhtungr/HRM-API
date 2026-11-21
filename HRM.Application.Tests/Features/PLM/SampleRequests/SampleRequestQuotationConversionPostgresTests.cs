using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestQuotationConversionPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task ConversionPersistsOnSameRequestWithAuditAndPreservesPriceObligation()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var name in new[] { "SampleRequest", "SampleRequestSampleTrial", "SampleRequestWorkflowEvent", "Employee", "AuditLog" })
            await SampleDeliveryOptionsPostgresTests.CreateTable(connection, db.Model.GetEntityTypes().Single(e => e.ClrType.Name == name));
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var date = new DateTime(2026, 11, 14, 8, 0, 0);
        db.Employees.Add(new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId!.Value, IsActive = true });
        var parents = new[] { false, true }.Select(accepted => new SampleRequest { SampleRequestId = Guid.NewGuid(),
            CompanyId = user.CompanyId!.Value, ManagerBy = user.EmployeeId.Value, ExternalId = accepted ? "TP_ACCEPTED" : "TP_QUOTE",
            InfoType = "Báo giá", Status = "InProgress", SampleQuantity = 0, ExpectedPrice = 25000, CreatedDate = date, UpdatedDate = date, IsActive = true }).ToArray();
        db.SampleRequests.AddRange(parents);
        foreach (var parent in parents)
            db.SampleRequestWorkflowEvents.Add(SampleRequestCustomerPriceAudit.CreateWorkflowMarker(parent.SampleRequestId, user.CompanyId.Value, user.EmployeeId.Value, date, parent.Status));
        var acceptance = SampleRequestCustomerPriceAudit.CreateEvent(parents[1].SampleRequestId, user.CompanyId.Value, user.EmployeeId.Value,
            date.AddMinutes(1), false, "InProgress", "InProgress", date, null, "QA accepted price", "Keep history");
        db.SampleRequestWorkflowEvents.Add(acceptance);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var handler = new PatchSampleRequestCommandHandler(db, user, SampleTrialFeedbackCorrectionTests.Visibility.Create(user), null!, null!, null!, new CurrentUserPermissionService(user));
        foreach (var parent in parents)
        {
            var command = new PatchSampleRequestCommand { SampleRequestId = parent.SampleRequestId, InfoType = "Thẻ màu (3)", SampleQuantity = 10, ExpectedUpdatedDate = date };
            var result = await handler.Handle(command, default);
            Assert.True(result.Success, result.Message); db.ChangeTracker.Clear();
            var saved = await db.SampleRequests.SingleAsync(x => x.SampleRequestId == parent.SampleRequestId);
            Assert.Equal("Thẻ màu (3)", saved.InfoType); Assert.Equal(10, saved.SampleQuantity); Assert.Equal("InProgress", saved.Status);
            var events = await db.SampleRequestWorkflowEvents.Where(e => e.SampleRequestId == parent.SampleRequestId).OrderByDescending(e => e.RecordedAt).ToArrayAsync();
            Assert.True(ChipThenSampleRules.NeedsChipRecord(saved, null, events, []));
            Assert.True(KgSampleCompletionRules.RequiresPriceAcceptance(saved, events));
            Assert.Single(events, e => e.EventType == SampleFlowRules.QuotationRequired);
            Assert.Single(events, e => e.EventType == SampleFlowRules.ChipThenSampleRequested);
            // A retried form carrying its old token cannot duplicate a plan or overwrite the saved state.
            Assert.False((await handler.Handle(command, default)).Success); db.ChangeTracker.Clear();
            Assert.Equal(2, await db.SampleRequestWorkflowEvents.CountAsync(e => e.SampleRequestId == parent.SampleRequestId &&
                (e.EventType == SampleFlowRules.QuotationRequired || e.EventType == SampleFlowRules.ChipThenSampleRequested)));
        }
        Assert.Equal(2, await db.SampleRequests.CountAsync());
        Assert.Equal(2, await db.AuditLogs.CountAsync());
        Assert.Equal("Keep history", (await db.SampleRequestWorkflowEvents.SingleAsync(e => e.Id == acceptance.Id)).Note);
        Assert.Empty(await db.SampleRequestSampleTrials.ToListAsync());
    }
}
