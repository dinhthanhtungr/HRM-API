using System.Reflection;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Application.Tests.Features.PLM.SampleRequests;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.Products;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaConfirmationTargetTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task ApprovalWithoutIdResolvesUniqueProductRequestButNeverGuessesBetweenTwo(bool multiple, bool explicitId, bool succeeds)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true };
        var formula = new Formula { FormulaId = Guid.NewGuid(), CompanyId = product.CompanyId,
            ProductId = product.ProductId, Product = product, Status = "Draft", CreatedDate = new(2026, 11, 1) };
        var target = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = product.CompanyId,
            ProductId = product.ProductId, Status = "InProgress", IsActive = true };
        var other = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = product.CompanyId,
            ProductId = multiple ? product.ProductId : Guid.NewGuid(), Status = "InProgress", IsActive = true };
        store.Seed(nameof(db.Formulas), formula);
        store.Seed(nameof(db.SampleRequests), target, other);
        var visibility = DispatchProxy.Create<ICustomerVisibilityService, Visibility>();
        var handler = new UpdateFormulaStatusCommandHandler(db, user, new FormulaWriteService(db, null!),
            new FormulaVersionService(db, new KeyedMutationLock<Guid>()), null!,
            new FormulaPricingReviewService(db, null!, null!), visibility, null!, new CurrentUserPermissionService(user), null!);
        var result = await handler.Handle(new(formula.FormulaId, new UpdateFormulaStatusRequest {
            Status = FormulaStatus.Approved, SampleRequestId = explicitId ? target.SampleRequestId : null,
            ExpectedUpdatedDate = formula.CreatedDate }), default);
        Assert.Equal(succeeds, result.Success);
        if (succeeds)
        {
            Assert.Equal("Approved", formula.Status);
            var evt = Assert.Single(store.Added.OfType<SampleRequestWorkflowEvent>());
            Assert.Equal(target.SampleRequestId, evt.SampleRequestId);
            Assert.Equal(formula.FormulaId, evt.FormulaId);
            Assert.Equal("LabFormulaConfirmed", evt.EventType);
            Assert.Equal("SampleRequestWorkflowNotification", Assert.Single(store.Added.OfType<OutboxMessage>()).Type);
            Assert.Single(store.Added.OfType<FormulaVersion>());
            Assert.Equal(1, store.Saves);
        }
        else
        {
            Assert.Contains("Multiple sample requests", result.Message);
            Assert.Equal("Draft", formula.Status);
            Assert.Empty(store.Added);
            Assert.Equal(0, store.Saves);
        }
    }

    [Fact]
    public async Task ResolutionPreservesVisibilityCompanyProductActiveAndCancelledBoundaries()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var company = Guid.NewGuid(); var product = Guid.NewGuid();
        SampleRequest Make(Guid co, Guid pr, bool active = true, string status = "InProgress")
            => new() { SampleRequestId = Guid.NewGuid(), CompanyId = co, ProductId = pr, IsActive = active, Status = status };
        var valid = Make(company, product);
        var hidden = Make(company, product);
        var invalid = new[] { Make(Guid.NewGuid(), product), Make(company, Guid.NewGuid()), Make(company, product, false), Make(company, product, status: "Cancelled") };
        store.Seed(nameof(db.SampleRequests), invalid.Concat([valid, hidden]).ToArray());
        var visible = db.SampleRequests.Where(x => x.SampleRequestId != hidden.SampleRequestId);
        Assert.Equal(valid.SampleRequestId, (await FormulaConfirmationTargetResolver.ResolveAsync(visible, company, product, null, default)).SampleRequestId);
        foreach (var row in invalid.Append(hidden))
            await Assert.ThrowsAsync<InvalidOperationException>(() => FormulaConfirmationTargetResolver.ResolveAsync(visible, company, product, row.SampleRequestId, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => FormulaConfirmationTargetResolver.ResolveAsync(visible, company, product, Guid.Empty, default));
    }

    [Fact]
    public void CandidateLookupTranslatesToBoundedScopedSql()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid(); var product = Guid.NewGuid(); var request = Guid.NewGuid();
        var sql = FormulaConfirmationTargetResolver.Candidates(db.SampleRequests, company, product, request).Take(2).ToQueryString();
        Assert.Contains(company.ToString(), sql); Assert.Contains(product.ToString(), sql); Assert.Contains(request.ToString(), sql);
        Assert.Contains("IsActive", sql); Assert.Contains("Cancelled", sql); Assert.Contains("LIMIT", sql);
    }

    [Fact]
    public void RestoredDraftRequiresLabConfirmationEvenWithOldPriceAndConfirmationHistory()
    {
        var decision = SampleWorkDecisionRules.Resolve(new SampleWorkFacts { Open = true, Quotation = true,
            FormulaStatus = "Draft", LabConfirmed = true, ApprovedPrice = true, AcceptedPrice = true,
            PriceNextStepRequired = true, Quoted = true });
        Assert.Equal("ApproveFormula", decision.Code);
        Assert.Equal("Lab", decision.Role);
    }

    public class Visibility : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "BuildScopeAsync" => Task.FromResult<ViewerScope>(null!),
            "ApplySampleRequestVisibility" => args![0],
            _ => throw new NotSupportedException(method.Name)
        };
    }
}
