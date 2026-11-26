using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkflowMutationTestSupport;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class InternalSampleCompletionPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [Theory]
    [InlineData("Nội bộ", "KH_VIETAUS", true)]
    [InlineData("Noi_bo", "KH_VIETAUS", true)]
    [InlineData("Internal", "KH_VIETAUS", true)]
    [InlineData("private", "KH_VIETAUS", true)]
    [InlineData("Khách hàng", "KH_VIETAUS", false)]
    [InlineData("private", "KH_OTHER", false)]
    [InlineData(null, "KH_VIETAUS", false)]
    public void DirectCompletionRequiresBothConditions(string? kind, string customer, bool allowed)
        => Assert.Equal(allowed, PLMInternalSampleRules.CanCompleteDirectly(kind, customer));

    [PostgresGroupStorageFact]
    public async Task RealHandlerCompletesOnlyTheExplicitInternalRequestAndSelectsItsVuWithoutDelivery()
    {
        await using var connection = await postgres.OpenAsync();
        var failure = new SnapshotFailure();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).AddInterceptors(failure).Options);
        await Schema(connection, db);
        var company = Guid.NewGuid(); var employee = Guid.NewGuid(); var actor = new Actor(company, employee, "LabUser");
        var at = new DateTime(2026,10,12,8,0,0); // No November cutoff for this special branch.
        var category = new Category { CategoryId = Guid.NewGuid(), CompanyId = company, IsActive = true, Name = "Test" };
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = company, CategoryId = category.CategoryId, IsActive = true, Name = "Internal", ColourCode = "INTERNAL_TEST" };
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, ExternalId = "KH_VIETAUS", CustomerName = "Internal", IsActive = true, IsLead = true };
        var formula = new Formula { FormulaId = Guid.NewGuid(), CompanyId = company, ProductId = product.ProductId,
            IsActive = true, Status = "Draft", CreatedDate = at, UpdatedDate = at, ExternalId = "VU_INTERNAL", Name = "F002" };
        var previous = new Formula { FormulaId = Guid.NewGuid(), CompanyId = company, ProductId = product.ProductId,
            IsActive = true, IsSelect = true, Status = "Completed", CreatedDate = at, UpdatedDate = at, ExternalId = "VU_PREVIOUS", Name = "F001" };
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, ProductId = product.ProductId,
            CustomerId = customer.CustomerId, ManagerBy = Guid.NewGuid(), RequestType = "Nội bộ", Status = "InProgress", IsActive = true,
            CreatedDate = at, UpdatedDate = at, SampleQuantity = 100, InfoType = "Báo giá", ExternalId = "TP_INTERNAL" };
        var otherRequest = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, ProductId = product.ProductId,
            CustomerId = customer.CustomerId, FormulaId = previous.FormulaId, ManagerBy = Guid.NewGuid(), RequestType = "Nội bộ", Status = "Completed",
            IsActive = true, CreatedDate = at, UpdatedDate = at, ExternalId = "TP_HISTORY" };
        db.Categories.Add(category); db.Products.Add(product); db.Customers.Add(customer); db.Formulas.AddRange(formula, previous);
        db.SampleRequests.AddRange(request, otherRequest); db.Employees.Add(new Employee { EmployeeId = employee, CompanyId = company, IsActive = true });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        UpdateFormulaStatusCommandHandler Handler(Actor user, bool visible = true) => new(db, user, new FormulaWriteService(db, null!),
            new FormulaVersionService(db, new KeyedMutationLock<Guid>()), null!, new FormulaPricingReviewService(db, db, null!),
            Visibility.Create(user, visible), null!, new CurrentUserPermissionService(user), db);
        UpdateFormulaStatusCommand Command(Guid? id = null, DateTime? formulaToken = null, DateTime? requestToken = null) => new(formula.FormulaId,
            new UpdateFormulaStatusRequest { Status = FormulaStatus.Completed, SampleRequestId = id ?? request.SampleRequestId,
                ExpectedUpdatedDate = formulaToken ?? at, ExpectedRequestUpdatedDate = requestToken ?? at });
        Assert.False((await Handler(new Actor(company, employee, "SaleUser")).Handle(Command(), default)).Success);
        Assert.False((await Handler(actor, false).Handle(Command(), default)).Success);
        Assert.False((await Handler(new Actor(Guid.NewGuid(), employee, "LabUser")).Handle(Command(), default)).Success);
        Assert.False((await Handler(actor).Handle(Command(formulaToken: at.AddDays(-1)), default)).Success);
        Assert.False((await Handler(actor).Handle(Command(requestToken: at.AddDays(-1)), default)).Success);
        Assert.False((await Handler(actor).Handle(new(formula.FormulaId, new() { Status = FormulaStatus.Completed, ExpectedUpdatedDate = at }), default)).Success);
        Assert.False((await Handler(actor).Handle(Command(otherRequest.SampleRequestId), default)).Success);
        foreach (var kind in new[] { "Khách hàng", "", "private" })
        {
            db.ChangeTracker.Clear(); var current = await db.SampleRequests.SingleAsync(x => x.SampleRequestId == request.SampleRequestId);
            current.RequestType = kind;
            if (kind == "private") { var c = await db.Customers.SingleAsync(); c.ExternalId = "KH_OTHER"; }
            await db.SaveChangesAsync();
            Assert.False((await Handler(actor).Handle(Command(), default)).Success);
        }
        db.ChangeTracker.Clear();
        (await db.Customers.SingleAsync()).ExternalId = "KH_VIETAUS";
        (await db.SampleRequests.SingleAsync(x => x.SampleRequestId == request.SampleRequestId)).RequestType = "Nội bộ";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var source = from x in db.SampleRequests.Where(x => x.SampleRequestId == request.SampleRequestId)
                     join t in db.SampleRequestSampleTrials on x.SampleRequestId equals t.SampleRequestId into history
                     from t in history.DefaultIfEmpty()
                     select new SampleRequestSampleTrialReportRow { SampleRequest = x, Trial = t,
                         ConfirmedFormulaId = x.FormulaId, LabFormulaConfirmedAt = x.LabReceivedDate,
                         HasPriceFeedback = false, HasAcceptedCustomerPrice = false, HasFinalizedChip = false };
        var facts = await SampleWorkDecisionQuery.ReadFacts(source, db, db, "VND").Select(x => x.Facts).SingleAsync();
        Assert.True(facts.InternalDirect);
        Assert.Equal("InternalFormulaCompletion", SampleWorkDecisionRules.Resolve(facts).Stage);
        failure.Enabled = true;
        Assert.False((await Handler(actor).Handle(Command(), default)).Success);
        failure.Enabled = false;
        db.ChangeTracker.Clear();
        Assert.Equal("InProgress", await db.SampleRequests.Where(x => x.SampleRequestId == request.SampleRequestId).Select(x => x.Status).SingleAsync());
        Assert.Equal("Draft", await db.Formulas.Where(x => x.FormulaId == formula.FormulaId).Select(x => x.Status).SingleAsync());
        Assert.Empty(await db.SampleRequestWorkflowEvents.ToListAsync());
        var result = await Handler(actor).Handle(Command(), default);
        Assert.True(result.Success, result.Message);
        db.ChangeTracker.Clear();
        var saved = await db.SampleRequests.SingleAsync(x => x.SampleRequestId == request.SampleRequestId);
        var selected = await db.Formulas.SingleAsync(x => x.FormulaId == formula.FormulaId);
        Assert.Equal("Completed", saved.Status); Assert.Equal(formula.FormulaId, saved.FormulaId);
        Assert.Equal("Completed", selected.Status); Assert.True(selected.IsSelect);
        Assert.Equal(employee, selected.CheckBy);
        Assert.Equal("INTERNAL_TEST", (await db.Products.SingleAsync()).ColourCode);
        Assert.Equal(previous.FormulaId, (await db.SampleRequests.SingleAsync(x => x.SampleRequestId == otherRequest.SampleRequestId)).FormulaId);
        Assert.Equal(at, (await db.SampleRequests.SingleAsync(x => x.SampleRequestId == otherRequest.SampleRequestId)).UpdatedDate);
        Assert.False((await db.Formulas.SingleAsync(x => x.FormulaId == previous.FormulaId)).IsSelect);
        Assert.Empty(await db.SampleRequestSampleTrials.ToListAsync()); Assert.Empty(await db.OutboxMessages.ToListAsync());
        Assert.Single(await db.SampleRequestWorkflowEvents.Where(x => x.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFlowRules.CompletedWithoutTrial).ToListAsync());
        Assert.False((await Handler(actor).Handle(Command(), default)).Success);
    }
    private sealed class SnapshotFailure : SaveChangesInterceptor
    {
        internal bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled) throw new DbUpdateException("Isolated snapshot failure");
            return new(result);
        }
    }

}
