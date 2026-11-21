using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleUnlinkedFollowUpPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task OldApprovedVuCanBeExplicitlyChosenWithoutInventingHistoricalConfirmation()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await SampleWorkflowMutationTestSupport.Schema(connection, db);
        var company = Guid.NewGuid(); var employee = Guid.NewGuid(); var product = Guid.NewGuid();
        var customer = Guid.NewGuid(); var category = Guid.NewGuid(); var requestId = Guid.NewGuid(); var formulaId = Guid.NewGuid();
        var date = new DateTime(2026, 11, 18, 9, 0, 0);
        foreach (var (name, values) in new (string, Dictionary<string, object?>)[] {
            ("Employee", new() { ["EmployeeId"] = employee, ["CompanyId"] = company, ["IsActive"] = true }),
            ("Customer", new() { ["CustomerId"] = customer, ["CompanyId"] = company, ["IsActive"] = true }),
            ("Category", new() { ["CategoryId"] = category, ["CompanyId"] = company, ["IsActive"] = true }),
            ("Product", new() { ["ProductId"] = product, ["CategoryId"] = category, ["CompanyId"] = company, ["IsActive"] = true }),
            ("Formula", new() { ["FormulaId"] = formulaId, ["ProductId"] = product, ["CompanyId"] = company, ["IsActive"] = true,
                ["Status"] = "Approved", ["CreatedDate"] = date.AddMonths(-1), ["UpdatedDate"] = date.AddMonths(-1), ["ExternalId"] = "VU_OLD_CONFIRMED" }),
            ("SampleRequest", new() { ["SampleRequestId"] = requestId, ["ProductId"] = product, ["CustomerId"] = customer,
                ["CompanyId"] = company, ["IsActive"] = true, ["Status"] = "InProgress", ["SampleQuantity"] = 1000d,
                ["InfoType"] = "Quotation", ["CreatedDate"] = date.AddMonths(-1), ["UpdatedDate"] = date })
        }) await Insert(connection, db.Model.GetEntityTypes().Single(e => e.ClrType.Name == name), values);
        foreach (var type in new[] { SampleFollowUpRules.Started, SampleFlowRules.KgRequested })
            await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestWorkflowEvent))!, new() {
                ["Id"] = Guid.NewGuid(), ["SampleRequestId"] = requestId, ["CompanyId"] = company,
                ["EventType"] = type, ["RecordedAt"] = date });
        var parent = await db.SampleRequests.SingleAsync(r => r.SampleRequestId == requestId);
        var state = await SampleFlowStateReader.ReadAsync(db, db, parent, true, false, default);
        Assert.Contains("Send", state.AllowedActions); Assert.Null(state.FormulaId);
        var attached = new HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials.SampleRequestSampleTrialReportDto { SampleRequestId = requestId };
        await SampleFlowStateReader.AttachAsync(db, [attached], company, employee, true, false, default);
        Assert.Contains("Send", attached.Flow!.AllowedActions); Assert.Equal("SendSample", attached.WorkDecision!.Code);
        var rows = from r in db.SampleRequests.Where(r => r.SampleRequestId == requestId)
                   join trial in db.SampleRequestSampleTrials on r.SampleRequestId equals trial.SampleRequestId into rounds
                   from trial in rounds.DefaultIfEmpty()
                   select new SampleRequestSampleTrialReportRow { SampleRequest = r, Trial = trial,
                       ConfirmedFormulaId = r.FormulaId, LabFormulaConfirmedAt = r.LabReceivedDate,
                       HasAcceptedCustomerPrice = false, HasFinalizedChip = false };
        var decision = await SampleWorkDecisionQuery.Enrich(rows, db, db, "VND").Select(r => r.WorkDecision!.Code).SingleAsync();
        Assert.Equal("SendSample", decision);
        Assert.Null((await db.SampleRequests.AsNoTracking().SingleAsync(r => r.SampleRequestId == requestId)).FormulaId);
        await db.Formulas.Where(f => f.FormulaId == formulaId).ExecuteUpdateAsync(set => set.SetProperty(f => f.Status, "Draft"));
        Assert.DoesNotContain("Send", (await SampleFlowStateReader.ReadAsync(db, db, parent, true, false, default)).AllowedActions);
        await db.Formulas.Where(f => f.FormulaId == formulaId).ExecuteUpdateAsync(set => set.SetProperty(f => f.Status, "Approved"));
        var actor = new SampleWorkflowMutationTestSupport.Actor(company, employee, ApplicationRoles.Lab.LabUser);
        var send = new SendSampleOptionsCommandHandler(db, db, actor, new CurrentUserPermissionService(actor),
            SampleWorkflowMutationTestSupport.Visibility.Create(actor), new Clock(date), new FormulaVersionService(db, new KeyedMutationLock<Guid>()));
        var command = new SendSampleOptionsCommand { SampleRequestId = requestId, BatchId = Guid.NewGuid(), SentDate = date,
            ExpectedRequestUpdatedDate = date, Options = [new(formulaId, 1000, date.AddMonths(-1))] };
        var result = await send.Handle(command, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(1, await db.SampleRequestSampleTrials.CountAsync(t => t.SampleRequestId == requestId && t.FormulaId == formulaId && t.DeliveredSampleQuantityKg == 1000));
        Assert.Equal(0, await db.SampleRequestWorkflowEvents.CountAsync(e => e.SampleRequestId == requestId && e.EventType == "LabFormulaConfirmed"));
    }
    private sealed class Clock(DateTime now) : IDateTimeProvider { public DateTime Now => now; }
}
