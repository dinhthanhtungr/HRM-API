using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDetail;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkPlanningDetailTests
{
    [Theory]
    [InlineData("LabAdmin", true, true)]
    [InlineData("LabAdmin", false, false)]
    [InlineData("SaleUser", true, false)]
    public async Task NonOwnerDetailRequiresActivePlanningIdentityAndKeepsPriceMasked(string role, bool active, bool visible)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User(role);
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var (crm, _) = ProductionOrderTestStore.CreateContext<ICustomerVisibilityReadDbContext>();
        var visibility = new CustomerVisibilityService(crm, user, new Clock());
        var item = Request(user.CompanyId!.Value);
        store.Seed(nameof(db.SampleRequests), item);
        store.Seed(nameof(db.Customers), item.Customer);
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId.Value, IsActive = active });
        store.Seed(nameof(db.Formulas),
            new Formula { FormulaId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, ProductId = item.ProductId, IsActive = true, TotalPrice = 123m },
            new Formula { FormulaId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), ProductId = item.ProductId, IsActive = true, TotalPrice = 456m });
        var handler = new GetSampleRequestDetailQueryHandler(db, new HiddenFields(), visibility, user, new CurrentUserPermissionService(user), HRM.Application.Tests.Features.CRM.CustomerCare.CustomerSampleSharingTestServices.Disabled());
        var result = await handler.Handle(new() { SampleRequestId = item.SampleRequestId }, default);
        Assert.Equal(visible, result != null);
        if (result != null)
        {
            Assert.Null(Assert.Single(result.FormulaLookups).TotalPrice);
            Assert.False(result.Hero.CanDeactivate);
        }
        Assert.False((await visibility.BuildReadScopeAsync()).HasFullCustomerView);
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task PlanningDetailAndAccessRejectForeignInactiveInternalAndBrokenRelationships()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabAdmin");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var (crm, _) = ProductionOrderTestStore.CreateContext<ICustomerVisibilityReadDbContext>();
        var visibility = new CustomerVisibilityService(crm, user, new Clock());
        var valid = Request(user.CompanyId!.Value);
        var foreign = Request(Guid.NewGuid());
        var inactive = Request(user.CompanyId.Value); inactive.IsActive = false;
        var internalByCode = Request(user.CompanyId.Value); internalByCode.Customer.ExternalId = "KH_VIETAUS";
        var internalById = Request(user.CompanyId.Value); internalById.CustomerId = internalById.Customer.CustomerId = CustomerVisibilityConstants.RestrictedCustomerId;
        var invalidProduct = Request(user.CompanyId.Value); invalidProduct.Product.CompanyId = Guid.NewGuid();
        store.Seed(nameof(db.SampleRequests), valid, foreign, inactive, internalByCode, internalById, invalidProduct);
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId.Value, IsActive = true });
        var handler = new GetSampleRequestDetailQueryHandler(db, new HiddenFields(), visibility, user, new CurrentUserPermissionService(user), HRM.Application.Tests.Features.CRM.CustomerCare.CustomerSampleSharingTestServices.Disabled());
        var access = new GetSampleRequestDetailAccessQueryHandler(db, visibility, user);
        Assert.NotNull(await handler.Handle(new() { SampleRequestId = valid.SampleRequestId }, default));
        Assert.Equal(SampleRequestDetailAccessStatus.NotFound,
            await access.Handle(new() { SampleRequestId = valid.SampleRequestId }, default));
        foreach (var item in new[] { foreign, inactive, internalByCode, internalById, invalidProduct })
            Assert.Null(await handler.Handle(new() { SampleRequestId = item.SampleRequestId }, default));
        Assert.Equal(SampleRequestDetailAccessStatus.NotFound, await access.Handle(new() { SampleRequestId = foreign.SampleRequestId }, default));
        Assert.Equal(SampleRequestDetailAccessStatus.NotFound, await access.Handle(new() { SampleRequestId = inactive.SampleRequestId }, default));
        Assert.Equal(0, store.Saves);
    }

    private static SampleRequest Request(Guid company)
    {
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = company, IsActive = true, Name = "Test" };
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true, ExternalId = "KH_TEST" };
        return new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            ProductId = product.ProductId, Product = product, CustomerId = customer.CustomerId, Customer = customer,
            ManagerBy = Guid.NewGuid(), ManagerByNavigation = new Employee { FullName = "Other owner" } };
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 3); }
    private sealed class HiddenFields : IPLMFieldVisibilityService
    {
        public bool CanViewFormulaPrices() => false;
        public bool CanViewFormulaMaterials() => false;
        public bool CanViewProductTechnicalInfo() => false;
        public bool CanViewMaterialPriceReviewDetails() => false;
    }
}
