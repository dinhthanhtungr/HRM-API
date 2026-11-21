using System.Reflection;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestLookup;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDetail;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class InternalSaleOrderSampleSelectionTests
{
    [Theory]
    [InlineData("PLPUUser", "valid")]
    [InlineData("LabAdmin", "valid")]
    [InlineData("PLPUUser", "unrestricted")]
    [InlineData("LabAdmin", "unrestricted")]
    [InlineData("PLPUUser", "foreign")]
    [InlineData("LabAdmin", "foreign")]
    [InlineData("PLPUUser", "inactive")]
    [InlineData("LabAdmin", "inactive")]
    [InlineData("PLPUUser", "new")]
    [InlineData("LabAdmin", "new")]
    [InlineData("PLPUUser", "external-order")]
    [InlineData("LabAdmin", "external-order")]
    [InlineData("PLPUUser", "merchandise")]
    [InlineData("LabAdmin", "merchandise")]
    public async Task LookupAndDetail_OnlyOpenEligibleSameCompanySamplesForInternalOrder(string role, string scenario)
    {
        var user = new User(role);
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var orderCustomer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            IsActive = true, ExternalId = scenario == "external-order" ? "KH_TEST" : "KH_VIETAUS" };
        var sourceCompany = scenario == "foreign" ? Guid.NewGuid() : user.CompanyId.Value;
        var sourceCustomer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = sourceCompany,
            IsActive = true, ExternalId = "KH_OTHER", CustomerName = "Other customer" };
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = sourceCompany,
            IsActive = scenario != "inactive", ColourCode = "TEST_COLOR", Name = "Test product" };
        var formula = new Formula { FormulaId = Guid.NewGuid(), ProductId = product.ProductId,
            CompanyId = sourceCompany, IsActive = true, ExternalId = "VU_TEST", TotalPrice = 123 };
        var sample = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = sourceCompany,
            CustomerId = sourceCustomer.CustomerId, Customer = sourceCustomer, ProductId = product.ProductId,
            Product = product, FormulaId = formula.FormulaId, Formula = formula, IsActive = true,
            Status = scenario == "new" ? "New" : "Completed", ExternalId = "SAMPLE_TEST",
            ManagerBy = Guid.NewGuid(), ManagerByNavigation = new Employee { FullName = "Other owner" },
            CreatedDate = new DateTime(2026, 10, 1) };
        store.Seed(nameof(db.Customers), orderCustomer, sourceCustomer);
        store.Seed(nameof(db.SampleRequests), sample);
        store.Seed(nameof(db.Formulas), formula);
        var visibility = DispatchProxy.Create<ICustomerVisibilityService, Visibility>();
        var proxy = (Visibility)(object)visibility;
        proxy.Scope = new ViewerScope(user.CompanyId.Value, user.EmployeeId!.Value, false, false,
            new HashSet<Guid>(), new HashSet<Guid> { user.EmployeeId.Value }, new DateTime(2026, 11, 5));
        var type = scenario == "merchandise" ? OrderType.Merchandise : OrderType.Internal;
        var items = await new GetSampleRequestLookupQueryHandler(db, user, visibility, HRM.Application.Tests.Features.CRM.CustomerCare.CustomerSampleSharingTestServices.Disabled()).Handle(new()
            { CustomerId = orderCustomer.CustomerId, ForSaleOrder = true, OrderType = type }, default);
        var detail = await new GetSampleRequestDetailQueryHandler(db, new Fields(), visibility, user, new Permissions(), HRM.Application.Tests.Features.CRM.CustomerCare.CustomerSampleSharingTestServices.Disabled())
            .Handle(new() { SampleRequestId = sample.SampleRequestId, CustomerId = orderCustomer.CustomerId,
                ForSaleOrder = true, OrderType = type }, default);
        var allowed = scenario is "valid" or "unrestricted";
        Assert.Equal(allowed ? 1 : 0, items.Count);
        Assert.Equal(allowed, detail is not null);
        if (allowed)
        {
            Assert.Equal(sample.SampleRequestId, Assert.Single(items).SampleRequestId);
            Assert.Equal(product.ProductId, detail!.QuickSummary.ProductId);
            Assert.Null(Assert.Single(detail.FormulaLookups).TotalPrice);
        }
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
    }

    public class Visibility : DispatchProxy
    {
        public ViewerScope Scope { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "BuildReadScopeAsync" => Task.FromResult(Scope),
            "ApplySampleRequestVisibility" => new CustomerVisibilityService(null!, null!, null!)
                .ApplySampleRequestVisibility((IQueryable<SampleRequest>)args![0]!,
                    (IQueryable<Customer>)args[1]!, (ViewerScope)args[2]!),
            _ => throw new NotSupportedException(method.Name)
        };
    }
    private sealed class Fields : IPLMFieldVisibilityService
    {
        public bool CanViewFormulaPrices() => false;
        public bool CanViewFormulaMaterials() => false;
        public bool CanViewProductTechnicalInfo() => false;
        public bool CanViewMaterialPriceReviewDetails() => false;
    }
    private sealed class Permissions : ICurrentUserPermissionService
    {
        public bool HasPermission(string permission) => false;
    }
    private sealed class User(string role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public bool IsInRole(string value) => value == role;
    }
}
