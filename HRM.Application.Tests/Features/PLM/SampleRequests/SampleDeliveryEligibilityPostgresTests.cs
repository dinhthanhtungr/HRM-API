using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkflowMutationTestSupport;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleDeliveryEligibilityPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task ChipSentFormulaCanLeadDraftAndCompletedOptionsWithoutApprovingDraftOrResendingKg()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await Schema(connection, db);
        var date = new DateTime(2026, 11, 15, 8, 0, 0);
        var lab = new Actor(Guid.NewGuid(), Guid.NewGuid(), "LabUser");
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = lab.CompanyId!.Value,
            ProductId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ManagerBy = lab.EmployeeId!.Value, IsActive = true,
            CreatedDate = date, UpdatedDate = date, Status = "SampleSent", InfoType = "Thẻ màu (1)", SampleQuantity = 2 };
        db.Customers.Add(new Customer { CustomerId = request.CustomerId, CompanyId = request.CompanyId, IsActive = true });
        var category = new Category { CategoryId = Guid.NewGuid(), CompanyId = request.CompanyId, IsActive = true, Name = "Test" };
        db.Categories.Add(category);
        db.Products.Add(new Product { ProductId = request.ProductId, CompanyId = request.CompanyId, CategoryId = category.CategoryId, IsActive = true, Name = "Test" });
        db.Employees.Add(new Employee { EmployeeId = lab.EmployeeId.Value, CompanyId = request.CompanyId, IsActive = true });
        db.SampleRequests.Add(request);
        var formulas = new[] { "SampleSent", "Draft", "Completed" }.Select((status, i) => new Formula {
            FormulaId = Guid.NewGuid(), CompanyId = request.CompanyId, ProductId = request.ProductId,
            Status = status, IsActive = true, CreatedDate = date, UpdatedDate = date, Name = "F00" + i, ExternalId = "VU_TEST_" + i }).ToArray();
        db.Formulas.AddRange(formulas);
        var chip = new SampleRequestSampleTrial { SampleRequestSampleTrialId = Guid.NewGuid(), SampleRequestId = request.SampleRequestId,
            FormulaId = formulas[0].FormulaId, TrialNo = 1, IsActive = true, CreatedDate = date, UpdatedDate = date,
            SentDate = date, Status = SampleTrialStatus.WaitingCustomerFeedback, DeliveredSampleQuantityKg = 0 };
        db.SampleRequestSampleTrials.Add(chip);
        db.SampleRequestWorkflowEvents.Add(SampleRequestWorkflowEvents.Create(request.SampleRequestId, request.CompanyId,
            lab.EmployeeId.Value, date.AddMinutes(-1), SampleFlowRules.ChipThenSampleRequested, formulaId: formulas[0].FormulaId));
        db.SampleRequestWorkflowEvents.Add(SampleRequestWorkflowEvents.Create(request.SampleRequestId, request.CompanyId,
            lab.EmployeeId.Value, date, SampleRequestWorkflowEvents.SampleSent, formulaId: formulas[0].FormulaId, trialId: chip.SampleRequestSampleTrialId));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var permissions = new CurrentUserPermissionService(lab); var visibility = Visibility.Create(lab);
        var get = new GetSampleDeliveriesQueryHandler(db, lab, permissions, visibility);
        var available = (await get.Handle(new(request.SampleRequestId, true), default))!;
        Assert.Equal(3, available.Candidates.Count);
        Assert.False(available.Candidates.Single(c => c.Status == "Draft").Confirmed);
        var send = new SendSampleOptionsCommandHandler(db, db, lab, permissions, visibility, new Clock(), new FormulaVersionService(db, new KeyedMutationLock<Guid>()));
        var command = new SendSampleOptionsCommand { SampleRequestId = request.SampleRequestId, BatchId = Guid.NewGuid(),
            ExpectedRequestUpdatedDate = date, SentDate = date.AddHours(1), Options = [new(formulas[1].FormulaId, 1, date)] };
        var draftOnly = await send.Handle(command, default);
        Assert.False(draftOnly.Success);
        Assert.Contains("primary sample", draftOnly.Message);
        Assert.Single(await db.SampleRequestSampleTrials.ToArrayAsync());
        command.Options = formulas.Select(f => new SendSampleOption(f.FormulaId, 1, date)).ToArray();
        var sent = await send.Handle(command, default);
        Assert.True(sent.Success, sent.Message); db.ChangeTracker.Clear();
        Assert.Equal(4, await db.SampleRequestSampleTrials.CountAsync());
        Assert.Equal(0, await db.SampleRequestWorkflowEvents.CountAsync(e => e.EventType == SampleRequestWorkflowEvents.FormulaConfirmed));
        Assert.Equal("SampleSent", (await db.Formulas.SingleAsync(f => f.FormulaId == formulas[1].FormulaId)).Status);
        Assert.Equal(SampleTrialStatus.WaitingCustomerFeedback, (await db.SampleRequestSampleTrials.SingleAsync(t => t.SampleRequestSampleTrialId == chip.SampleRequestSampleTrialId)).Status);
        Assert.Empty((await get.Handle(new(request.SampleRequestId, true), default))!.Candidates);
        command.BatchId = Guid.NewGuid(); command.ExpectedRequestUpdatedDate = (await db.SampleRequests.SingleAsync()).UpdatedDate;
        Assert.False((await send.Handle(command, default)).Success);
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 16, 10, 0, 0); }
}
