using System.Reflection;
using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestLookup;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDetail;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.CRM.CustomerCare;

public sealed class CustomerSampleSharingTests
{
    [Theory]
    [InlineData("same", true)]
    [InlineData("different", false)]
    [InlineData("inactive-group", false)]
    [InlineData("inactive-member", false)]
    [InlineData("inactive-customer", false)]
    [InlineData("foreign-group", false)]
    [InlineData("foreign-customer", false)]
    public async Task Resolve_OnlyActiveMembersOfSameCompanyGroupAreShared(string scenario, bool shared)
    {
        var f = new Fixture();
        if (scenario == "different") f.MemberB.GroupId = Guid.NewGuid();
        if (scenario == "inactive-group") f.Group.IsActive = false;
        if (scenario == "inactive-member") f.MemberB.IsActive = false;
        if (scenario == "inactive-customer") f.B.IsActive = false;
        if (scenario == "foreign-group") f.Group.CompanyId = Guid.NewGuid();
        if (scenario == "foreign-customer") f.B.CompanyId = Guid.NewGuid();
        var owners = await f.Service.ResolveOwnerIdsAsync(f.Company, f.A.CustomerId, default);
        Assert.NotNull(owners);
        Assert.Contains(f.A.CustomerId, owners!);
        Assert.Equal(shared, owners!.Contains(f.B.CustomerId));
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task GroupMembershipDoesNotGrantAccessToOrderingCustomer()
    {
        var f = new Fixture();
        Assert.Null(await f.Service.ResolveOwnerIdsAsync(f.Company, f.B.CustomerId, default));
        Assert.Null(await f.Service.ResolveOwnerIdsAsync(Guid.NewGuid(), f.A.CustomerId, default));
    }

    [Fact]
    public async Task DisabledFeatureDoesNotQueryNewTables()
        => Assert.Equal(new[] { Guid.Empty }, await CustomerSampleSharingTestServices.Disabled().ResolveOwnerIdsAsync(Guid.Empty, Guid.Empty, default));

    [Theory]
    [InlineData("LabUser")]
    [InlineData("PLPUUser")]
    [InlineData("ACCUser")]
    public async Task OrdinaryRolesCannotManageSharing(string role)
    {
        var f = new Fixture(); f.User.Roles = [role];
        var permissions = new CurrentUserPermissionService(f.User);
        Assert.False((await new AssignCustomerSampleSharingGroupCommandHandler(f.Db, f.User, permissions, new Clock(), f.Options, f.Service)
            .Handle(new(f.A.CustomerId, new(f.Group.Id)), default)).Success);
        Assert.False((await new CreateCustomerSampleSharingGroupCommandHandler(f.Db, f.User, permissions, new Clock(), f.Options)
            .Handle(new(new("Test")), default)).Success);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task MovingCustomerSoftClosesOldMembershipAndAuditsAtomically()
    {
        var f = new Fixture();
        var target = new CustomerSampleSharingGroup { CompanyId = f.Company, Name = "Target" };
        f.Store.Seed(nameof(f.Db.CustomerSampleSharingGroups), f.Group, target);
        var result = await new AssignCustomerSampleSharingGroupCommandHandler(f.Db, f.User, new CurrentUserPermissionService(f.User), new Clock(), f.Options, f.Service)
            .Handle(new(f.A.CustomerId, new(target.Id)), default);
        Assert.True(result.Success, result.Message);
        Assert.False(f.MemberA.IsActive);
        Assert.Equal(Clock.NowValue, f.MemberA.RemovedAt);
        Assert.Equal(target.Id, Assert.Single(f.Store.Added.OfType<CustomerSampleSharingMember>()).GroupId);
        Assert.Single(f.Store.Added.OfType<HRM.Domain.Entities.AuditSchema.AuditLog>());
        Assert.True(f.Store.Transaction.Committed);
        Assert.Equal(1, f.Store.Locks);
    }

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("SaleAdmin")]
    [InlineData("Leader")]
    public async Task SalesCannotUnlinkGroupWithOutOfScopeCustomer(string role)
    {
        var f = new Fixture(); f.User.Roles = [role];
        var result = await new AssignCustomerSampleSharingGroupCommandHandler(f.Db, f.User,
            new CurrentUserPermissionService(f.User), new Clock(), f.Options, f.Service)
            .Handle(new(f.A.CustomerId, new(null)), default);
        Assert.False(result.Success);
        Assert.Equal(0, f.Store.Saves);
        Assert.True(f.MemberA.IsActive);
    }

    [Fact]
    public async Task SaleWorkspaceHidesOutOfScopeGroupsAndExplainsRestrictedMembership()
    {
        var f = new Fixture(); f.User.Roles = ["SaleUser"];
        var result = await new GetCustomerSampleSharingQueryHandler(f.Db, f.User,
            new CurrentUserPermissionService(f.User), f.Options, f.Service).Handle(new(), default);
        Assert.True(result.Success);
        Assert.Empty(result.Data!.Groups);
        Assert.Equal(f.A.CustomerId, Assert.Single(result.Data.Customers!).CustomerId);
        Assert.Contains(f.A.CustomerId, result.Data.RestrictedCustomerIds!);
    }

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("SaleAdmin")]
    [InlineData("Leader")]
    public async Task SalesCanAtomicallyLinkTwoManagedCustomers(string role)
    {
        var f = new Fixture(); f.User.Roles = [role];
        ((VisibilityProxy)(object)f.Visibility).AllCustomers = true;
        f.Store.Seed<CustomerSampleSharingMember>(nameof(f.Db.CustomerSampleSharingMembers));
        var result = await new LinkCustomerSamplesCommandHandler(f.Db, f.User, new CurrentUserPermissionService(f.User), new Clock(), f.Service)
            .Handle(new(f.A.CustomerId, new(f.B.CustomerId, "Branches")), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(2, f.Store.Added.OfType<CustomerSampleSharingMember>().Count());
        Assert.Single(f.Store.Added.OfType<CustomerSampleSharingGroup>());
        Assert.Single(f.Store.Added.OfType<HRM.Domain.Entities.AuditSchema.AuditLog>());
        Assert.True(f.Store.Transaction.Committed);
        Assert.Equal(1, f.Store.Saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LinkRejectsOutOfScopeOrSeparateGroupsWithoutSaving(bool separateGroups)
    {
        var f = new Fixture(); f.User.Roles = ["SaleUser"];
        if (separateGroups)
        {
            ((VisibilityProxy)(object)f.Visibility).AllCustomers = true;
            f.MemberB.GroupId = Guid.NewGuid();
        }
        var result = await new LinkCustomerSamplesCommandHandler(f.Db, f.User, new CurrentUserPermissionService(f.User), new Clock(), f.Service)
            .Handle(new(f.A.CustomerId, new(f.B.CustomerId, "Branches")), default);
        Assert.False(result.Success);
        Assert.Equal(0, f.Store.Saves);
        Assert.Empty(f.Store.Added);
        Assert.False(f.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("inactive")]
    public async Task InvalidTargetGroupDoesNotRemoveExistingMembership(string scenario)
    {
        var f = new Fixture();
        if (scenario == "foreign") f.Group.CompanyId = Guid.NewGuid(); else f.Group.IsActive = false;
        var result = await new AssignCustomerSampleSharingGroupCommandHandler(f.Db, f.User, new CurrentUserPermissionService(f.User), new Clock(), f.Options, f.Service)
            .Handle(new(f.A.CustomerId, new(f.Group.Id)), default);
        Assert.False(result.Success);
        Assert.True(f.MemberA.IsActive);
        Assert.False(f.Store.Transaction.Committed);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public void RemovingGroupRequiresExplicitNull()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AssignSampleSharingGroupRequest>("{}"));
        Assert.Null(JsonSerializer.Deserialize<AssignSampleSharingGroupRequest>("{\"GroupId\":null}")!.GroupId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectCreateOrderRejectsUnsharedSampleBeforeAnySideEffect(bool removed)
    {
        var f = new Fixture();
        if (removed) f.MemberB.IsActive = false;
        var outside = new Customer { CustomerId = Guid.NewGuid(), CompanyId = f.Company, IsActive = true, ExternalId = "OUT" };
        var owner = removed ? f.B : outside;
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = f.Company, IsActive = true };
        f.Store.Seed(nameof(f.Db.SampleRequests), new SampleRequest { CompanyId = f.Company, IsActive = true, Status = "Completed",
            CustomerId = owner.CustomerId, Customer = owner, ProductId = product.ProductId, Product = product });
        var (db, store) = ProductionOrderTestStore.CreateContext<HRM.Application.Abstractions.Persistence.PLM.SaleOrders.ISaleOrderDbContext>();
        store.Seed(nameof(db.Customers), f.A);
        store.Seed(nameof(db.Employees), new HRM.Domain.Entities.HrSchema.Employee { EmployeeId = f.User.EmployeeId!.Value,
            CompanyId = f.Company, IsActive = true, FullName = "Sale", ExternalId = "EMP" });
        var service = new HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrder.Services.SaleOrderCreationService(
            db, null!, null!, null!, null!, f.Service);
        var result = await service.CreateAsync(new() { CustomerId = f.A.CustomerId, OrderType = OrderType.Merchandise,
            SaleOrderDetails = [new() { ProductId = product.ProductId, FormulaId = Guid.NewGuid(), ExpectedQuantity = 1, UnitPriceAgreed = 1 }] },
            f.User.EmployeeId.Value, f.Company, Clock.NowValue, default);
        Assert.False(result.Success);
        Assert.Equal("Mẫu không thuộc khách đặt hàng hoặc nhóm dùng chung mẫu còn hoạt động.", result.Message);
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
        Assert.Equal(1, f.Store.Locks);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaleOrderLookupAndDetailUseSharingWithoutExposingOwnerPricesOrFiles(bool shared)
    {
        var f = new Fixture();
        if (!shared) f.MemberB.IsActive = false;
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = f.Company, IsActive = true, ColourCode = "COLOR", Name = "Shared" };
        var formula = new Formula { FormulaId = Guid.NewGuid(), ProductId = product.ProductId, CompanyId = f.Company, IsActive = true, Status = "Completed", TotalPrice = 999 };
        var sample = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = f.Company, CustomerId = f.B.CustomerId, Customer = f.B,
            ProductId = product.ProductId, Product = product, FormulaId = formula.FormulaId, Formula = formula,
            IsActive = true, Status = "Completed", ExternalId = "REQ", ExpectedPrice = 123, AttachmentCollectionId = Guid.NewGuid(),
            ManagerByNavigation = new() { FullName = "Owner" } };
        store.Seed(nameof(db.Customers), f.A, f.B); store.Seed(nameof(db.Products), product);
        store.Seed(nameof(db.Formulas), formula); store.Seed(nameof(db.SampleRequests), sample);
        f.Store.Seed(nameof(f.Db.SampleRequests), sample);
        var lookup = await new GetSampleRequestLookupQueryHandler(db, f.User, f.Visibility, f.Service).Handle(new()
            { CustomerId = f.A.CustomerId, ForSaleOrder = true, OrderType = OrderType.Merchandise }, default);
        var detail = await new GetSampleRequestDetailQueryHandler(db, new Fields(), f.Visibility, f.User,
            new CurrentUserPermissionService(f.User), f.Service).Handle(new()
                { CustomerId = f.A.CustomerId, SampleRequestId = sample.SampleRequestId, ForSaleOrder = true, OrderType = OrderType.Merchandise }, default);
        Assert.Equal(shared ? 1 : 0, lookup.Count);
        Assert.Equal(shared, detail is not null);
        if (detail is not null)
        {
            Assert.Null(detail.QuickSummary.ExpectedPrice);
            Assert.Empty(detail.Attachments);
            Assert.Equal(Guid.Empty, detail.QuickSummary.AttachmentCollectionId);
            Assert.Null(Assert.Single(detail.FormulaLookups).TotalPrice);
            Assert.False(detail.Hero.CanDeactivate);
        }
        var eligible = await f.Service.EligibleProductsAsync(f.Company, shared ? [f.A.CustomerId, f.B.CustomerId] : [f.A.CustomerId], [product.ProductId], default);
        Assert.Equal(shared ? 1 : 0, eligible.Length);
        var formulas = await new HRM.Application.Features.PLM.Formulas.Services.FormulaListQueryService(db, new Fields(), f.User, f.Service)
            .GetAsync(new() { ProductId = product.ProductId, CustomerId = f.A.CustomerId, IsMerchadiseOrder = true, OrderType = OrderType.Merchandise });
        Assert.Equal(shared ? 1 : 0, formulas.FormulaDevs.Count);
        if (shared) Assert.Null(Assert.Single(formulas.FormulaDevs).Price);
    }

    private sealed class Fields : IPLMFieldVisibilityService
    {
        public bool CanViewFormulaPrices() => true;
        public bool CanViewFormulaMaterials() => false;
        public bool CanViewProductTechnicalInfo() => false;
        public bool CanViewMaterialPriceReviewDetails() => false;
    }
    [Fact]
    public async Task BatchMovesMultipleMembersAndCommitsOneTransaction()
    {
        var f = new Fixture();
        var source = new CustomerSampleSharingGroup { CompanyId = f.Company, Name = "Source" };
        f.MemberB.GroupId = source.Id; f.MemberB.Group = source;
        f.Store.Seed(nameof(f.Db.CustomerSampleSharingGroups), f.Group, source);
        var revision = await SampleSharingRevision.ReadAsync(f.Db, f.Company, default);
        var result = await Batch(f).Handle(new(new(f.Group.Id, f.A.CustomerId, revision, "Renamed", [f.A.CustomerId, f.B.CustomerId])), default);
        Assert.True(result.Success, result.Message); Assert.True(f.Store.Transaction.Committed);
        Assert.False(f.MemberB.IsActive); Assert.True(f.MemberA.IsActive);
        var member = Assert.Single(f.Store.Added.OfType<CustomerSampleSharingMember>());
        Assert.Equal(f.B.CustomerId, member.CustomerId); Assert.Equal(f.Group.Id, member.GroupId);
        Assert.Equal(2, f.Store.Saves); Assert.Equal(1, f.Store.Locks);
        Assert.Single(f.Store.Added.OfType<HRM.Domain.Entities.AuditSchema.AuditLog>());
    }

    [Fact]
    public async Task BatchRejectsStaleRevisionBeforeWriting()
    {
        var f = new Fixture(); var revision = await SampleSharingRevision.ReadAsync(f.Db, f.Company, default);
        f.MemberB.IsActive = false;
        var result = await Batch(f).Handle(new(new(f.Group.Id, f.A.CustomerId, revision, f.Group.Name, [f.A.CustomerId])), default);
        Assert.False(result.Success); Assert.Equal(0, f.Store.Saves); Assert.False(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task WorkspaceExplainsAnInactiveMemberBlockEvenToAnAdministrator()
    {
        var f = new Fixture(); f.B.IsActive = false;
        var result = await new GetCustomerSampleSharingQueryHandler(f.Db, f.User, new CurrentUserPermissionService(f.User), f.Options, f.Service)
            .Handle(new GetCustomerSampleSharingQuery(), default);
        Assert.True(result.Success); Assert.False(Assert.Single(result.Data!.Groups).CanEdit);
        Assert.Contains(f.A.CustomerId, result.Data.RestrictedCustomerIds!); Assert.Equal(0, f.Store.Saves);
    }

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("Leader")]
    public async Task BatchRejectsSourceGroupOutsideSalesScopeWithoutPartialWrites(string role)
    {
        var f = new Fixture(); f.User.Roles = [role];
        var revision = await SampleSharingRevision.ReadAsync(f.Db, f.Company, default);
        var result = await Batch(f).Handle(new(new(f.Group.Id, f.A.CustomerId, revision, f.Group.Name, [f.A.CustomerId])), default);
        Assert.False(result.Success); Assert.Equal(0, f.Store.Saves); Assert.True(f.MemberB.IsActive);
    }

    [Fact]
    public async Task BatchSaleMayEditOnlyAnAnchoredManagedGroup()
    {
        var f = new Fixture(); f.User.Roles = ["SaleUser"]; ((VisibilityProxy)(object)f.Visibility).AllCustomers = true;
        var revision = await SampleSharingRevision.ReadAsync(f.Db, f.Company, default);
        var denied = await Batch(f).Handle(new(new(f.Group.Id, null, revision, f.Group.Name, [f.A.CustomerId])), default);
        Assert.False(denied.Success); Assert.Equal(0, f.Store.Saves);
        var result = await Batch(f).Handle(new(new(f.Group.Id, f.A.CustomerId, revision, f.Group.Name, [f.A.CustomerId])), default);
        Assert.True(result.Success, result.Message); Assert.False(f.MemberB.IsActive); Assert.True(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task BatchRejectsForeignCustomerAndDuplicateIdsBeforeChangingMembership()
    {
        var f = new Fixture(); var revision = await SampleSharingRevision.ReadAsync(f.Db, f.Company, default);
        var foreign = await Batch(f).Handle(new(new(f.Group.Id, f.A.CustomerId, revision, f.Group.Name, [f.A.CustomerId, Guid.NewGuid()])), default);
        var duplicate = await Batch(f).Handle(new(new(f.Group.Id, f.A.CustomerId, revision, f.Group.Name, [f.A.CustomerId, f.A.CustomerId])), default);
        Assert.False(foreign.Success); Assert.False(duplicate.Success); Assert.Equal(0, f.Store.Saves);
        Assert.True(f.MemberA.IsActive); Assert.True(f.MemberB.IsActive);
    }

    [Fact]
    public async Task BatchSecondSaveFailureRollsBackTransaction()
    {
        var f = new Fixture(); f.Store.FailAtSave = 2;
        var revision = await SampleSharingRevision.ReadAsync(f.Db, f.Company, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Batch(f).Handle(new(new(f.Group.Id, f.A.CustomerId, revision, f.Group.Name, [f.A.CustomerId])), default));
        Assert.False(f.Store.Transaction.Committed); Assert.True(f.Store.Transaction.RolledBack);
    }

    [Fact]
    public async Task BatchRejectsAnInactiveSourceGroupBeforeAnyWrite()
    {
        var f = new Fixture(); var source = new CustomerSampleSharingGroup { CompanyId = f.Company, Name = "Inactive", IsActive = false };
        f.MemberB.GroupId = source.Id; f.MemberB.Group = source;
        f.Store.Seed(nameof(f.Db.CustomerSampleSharingGroups), f.Group, source);
        var revision = await SampleSharingRevision.ReadAsync(f.Db, f.Company, default);
        var result = await Batch(f).Handle(new(new(f.Group.Id, f.A.CustomerId, revision, f.Group.Name, [f.A.CustomerId, f.B.CustomerId])), default);
        Assert.False(result.Success); Assert.Equal(0, f.Store.Saves); Assert.True(f.MemberB.IsActive);
    }

    [Fact]
    public void BatchRequiresExplicitMembershipAndRevisionFields()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SaveSampleSharingMembersRequest>("{}"));
    }

    private static SaveSampleSharingMembersCommandHandler Batch(Fixture f)
        => new(f.Db, f.User, new CurrentUserPermissionService(f.User), new Clock(), f.Service);

    [Fact]
    public async Task BatchCreatesAnInternalNameWithoutUserInput()
    {
        var f = new Fixture(); var revision = await SampleSharingRevision.ReadAsync(f.Db, f.Company, default);
        var result = await Batch(f).Handle(new(new(null, null, revision, null, [f.A.CustomerId, f.B.CustomerId])), default);
        Assert.True(result.Success, result.Message);
        var group = Assert.Single(f.Store.Added.OfType<CustomerSampleSharingGroup>());
        Assert.StartsWith("LK-", group.Name); Assert.Equal("Shared", f.Group.Name);
        Assert.True(f.Store.Transaction.Committed);
    }

    [Fact]
    public void BatchAllowsNameToBeOmittedFromJson()
    {
        var json = JsonSerializer.Serialize(new { GroupId = (Guid?)null, AnchorCustomerId = (Guid?)null, Revision = "r", CustomerIds = Array.Empty<Guid>() });
        var request = JsonSerializer.Deserialize<SaveSampleSharingMembersRequest>(json);
        Assert.NotNull(request); Assert.Null(request.Name);
    }

    [Fact]
    public async Task SharedSamplesKeepOrderingCustomerAndDoNotGrantQuotationEligibility()
    {
        var f = new Fixture(); var sender = DispatchProxy.Create<MediatR.ISender, SamplesSender>();
        var proxy = (SamplesSender)(object)sender;
        proxy.OrderSamples = [new() { SampleRequestId = Guid.NewGuid(), CustomerId = f.B.CustomerId, CustomerExternalId = "B",
            ProductId = Guid.NewGuid(), ProductCode = "BLUE", FormulaId = Guid.NewGuid(), ExternalId = "TP01" }];
        var result = await new GetCustomerSharedSamplesQueryHandler(f.User, f.Service, sender, f.Db).Handle(new(f.A.CustomerId, "BLUE"), default);
        Assert.True(result.Success); var row = Assert.Single(result.Data!);
        Assert.True(row.CanOrder); Assert.False(row.CanQuote); Assert.Equal("quotationCustomerMismatch", row.QuoteDisabledReason);
        Assert.Equal(f.B.CustomerId, row.OwnerCustomerId); Assert.Equal("A", row.TargetCode);
        Assert.Equal(2, proxy.Calls);
    }

    [Fact]
    public async Task SharedSamplesRejectUnauthorizedOrderingCustomerBeforeLookup()
    {
        var f = new Fixture(); var sender = DispatchProxy.Create<MediatR.ISender, SamplesSender>();
        var result = await new GetCustomerSharedSamplesQueryHandler(f.User, f.Service, sender, f.Db).Handle(new(f.B.CustomerId, null), default);
        Assert.False(result.Success); Assert.Equal(0, ((SamplesSender)(object)sender).Calls);
    }

    public class SamplesSender : DispatchProxy
    {
        public IReadOnlyList<HRM.Application.Features.PLM.SampleRequests.Dtos.FormOptions.SampleRequestLookupItemDto> OrderSamples { get; set; } = [];
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls++;
            var query = (GetSampleRequestLookupQuery)args![0]!;
            return Task.FromResult(query.ForSaleOrder ? OrderSamples : (IReadOnlyList<HRM.Application.Features.PLM.SampleRequests.Dtos.FormOptions.SampleRequestLookupItemDto>)[]);
        }
    }

    [Fact]
    public async Task QuickLinkCreatesInternalNameWithoutMovingExistingMemberships()
    {
        var f = new Fixture();
        f.Store.Seed<CustomerSampleSharingMember>(nameof(f.Db.CustomerSampleSharingMembers));
        var result = await new LinkCustomerSamplesCommandHandler(f.Db, f.User, new CurrentUserPermissionService(f.User), new Clock(), f.Service)
            .Handle(new(f.A.CustomerId, new(f.B.CustomerId, null)), default);
        Assert.True(result.Success, result.Message);
        Assert.StartsWith("LK-", Assert.Single(f.Store.Added.OfType<CustomerSampleSharingGroup>()).Name);
        Assert.Equal(2, f.Store.Added.OfType<CustomerSampleSharingMember>().Count());
        Assert.True(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task AdminRoleCanOpenWorkspaceAndLinkAcrossCompanyScope()
    {
        var f = new Fixture(); f.User.Roles = ["Admin"];
        f.Store.Seed<CustomerSampleSharingMember>(nameof(f.Db.CustomerSampleSharingMembers));
        var permissions = new CurrentUserPermissionService(f.User);
        var workspace = await new GetCustomerSampleSharingQueryHandler(f.Db, f.User, permissions, f.Options, f.Service).Handle(new(), default);
        Assert.True(workspace.Success); Assert.True(workspace.Data!.CanAdmin);
        Assert.Equal(2, workspace.Data.Customers!.Count);
        var result = await new LinkCustomerSamplesCommandHandler(f.Db, f.User, permissions, new Clock(), f.Service)
            .Handle(new(f.A.CustomerId, new(f.B.CustomerId, null)), default);
        Assert.True(result.Success, result.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QuickLinkRejectsSelfOrDuplicateBeforeSaving(bool self)
    {
        var f = new Fixture();
        var result = await new LinkCustomerSamplesCommandHandler(f.Db, f.User, new CurrentUserPermissionService(f.User), new Clock(), f.Service)
            .Handle(new(f.A.CustomerId, new(self ? f.A.CustomerId : f.B.CustomerId, null)), default);
        Assert.False(result.Success);
        Assert.Equal(0, f.Store.Saves);
        Assert.Empty(f.Store.Added);
    }

    [Fact]
    public async Task WorkspaceIncludesTaxNumberOnlyForScopedCustomers()
    {
        var f = new Fixture(); f.A.TaxNumber = "TEST-TAX";
        var result = await new GetCustomerSampleSharingQueryHandler(f.Db, f.User, new CurrentUserPermissionService(f.User), f.Options, f.Service)
            .Handle(new(), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal("TEST-TAX", result.Data!.Customers!.Single(customer => customer.CustomerId == f.A.CustomerId).TaxNumber);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QuickLinkUsesExplicitCapabilitiesInsteadOfRoleNames(bool grantManage)
    {
        var f = new Fixture(); f.User.Roles = ["SaleUser"];
        f.User.HasExplicitPermissionSet = true;
        f.User.Permissions = grantManage
            ? [ApplicationPermissions.CRM.AdminCustomerSampleSharing, ApplicationPermissions.CRM.ManageCustomerSampleSharing]
            : [ApplicationPermissions.CRM.AdminCustomerSampleSharing];
        f.Store.Seed<CustomerSampleSharingMember>(nameof(f.Db.CustomerSampleSharingMembers));
        var result = await new LinkCustomerSamplesCommandHandler(f.Db, f.User, new CurrentUserPermissionService(f.User), new Clock(), f.Service)
            .Handle(new(f.A.CustomerId, new(f.B.CustomerId, null)), default);
        Assert.Equal(grantManage, result.Success);
        Assert.Equal(grantManage ? 1 : 0, f.Store.Saves);
    }

    [Fact]
    public async Task WorkspaceReturnsNewestMembershipFirst()
    {
        var f = new Fixture();
        f.MemberA.CreatedAt = Clock.NowValue.AddDays(-1);
        f.MemberB.CreatedAt = Clock.NowValue;
        var result = await new GetCustomerSampleSharingQueryHandler(f.Db, f.User, new CurrentUserPermissionService(f.User), f.Options, f.Service).Handle(new(), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { f.B.CustomerId, f.A.CustomerId }, Assert.Single(result.Data!.Groups).Members.Select(member => member.CustomerId));
    }

    [Fact]
    public async Task BatchUnlinksOnlyMembershipAndKeepsCustomersAndOtherMembers()
    {
        var f = new Fixture();
        var revision = await SampleSharingRevision.ReadAsync(f.Db, f.Company, default);
        var result = await Batch(f).Handle(new(new(f.Group.Id, f.A.CustomerId, revision, f.Group.Name, [f.A.CustomerId])), default);
        Assert.True(result.Success, result.Message);
        Assert.True(f.MemberA.IsActive);
        Assert.False(f.MemberB.IsActive);
        Assert.NotNull(f.MemberB.RemovedAt);
        Assert.True(f.A.IsActive); Assert.True(f.B.IsActive);
        Assert.Empty(f.Store.Added.OfType<CustomerSampleSharingMember>());
        Assert.Single(f.Store.Added.OfType<HRM.Domain.Entities.AuditSchema.AuditLog>());
        Assert.True(f.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData("shared", true)]
    [InlineData("unlinked", false)]
    [InlineData("disabled", false)]
    [InlineData("foreign", false)]
    [InlineData("inactive", false)]
    [InlineData("denied", false)]
    [InlineData("semi", false)]
    public async Task CustomerScopedQuoteLookupAndLineValidationUseTheSameActiveGroup(string scenario, bool expected)
    {
        var f = new Fixture(); f.User.Roles = ["SaleUser"];
        if (scenario == "unlinked") f.MemberB.IsActive = false;
        if (scenario == "disabled") f.Options.Value.Enabled = false;
        if (scenario == "foreign") f.B.CompanyId = Guid.NewGuid();
        if (scenario == "inactive") f.B.IsActive = false;
        if (scenario == "denied") ((VisibilityProxy)(object)f.Visibility).CustomerId = Guid.NewGuid();
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = f.Company, IsActive = true, ColourCode = "BP51075", Name = "Shared test", Unit = "kg" };
        var sample = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = f.Company, CustomerId = f.B.CustomerId, Customer = f.B,
            ProductId = product.ProductId, Product = product, IsActive = true, Status = "Completed", ExternalId = "TEST-REQ",
            RequestType = scenario == "semi" ? HRM.Application.Features.PLM.Shared.Rules.SemiFinishedRequestRules.RequestType : string.Empty };
        var (read, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        store.Seed(nameof(read.Customers), f.A, f.B); store.Seed(nameof(read.Products), product); store.Seed(nameof(read.SampleRequests), sample);
        var lookup = new GetSampleRequestLookupQueryHandler(read, f.User, f.Visibility, f.Service);
        var found = await lookup.Handle(new() { CustomerId = f.A.CustomerId, ForSaleOrder = false }, default);
        Assert.Equal(expected ? 1 : 0, found.Count);
        if (expected) Assert.Equal(f.B.CustomerId, Assert.Single(found).CustomerId);
        Assert.Empty(await lookup.Handle(new() { ForSaleOrder = false }, default));
        var (crm, crmStore) = ProductionOrderTestStore.CreateContext<ICRMReadDbContext>();
        crmStore.Seed(nameof(crm.Products), product); crmStore.Seed(nameof(crm.SampleRequests), sample);
        var builder = new HRM.Application.Features.CRM.Quotations.Services.QuotationLineBuilder(crm, f.Service);
        var built = await builder.BuildAsync(Guid.NewGuid(), f.Company, f.A.CustomerId, "VND", 1,
            [new() { ProductId = product.ProductId, SampleRequestId = sample.SampleRequestId, Quantity = 10, UnitPrice = 1234,
                PriceTiers = [new() { QuantityRangeLabel = "all", UnitPrice = 1234 }] }], default);
        Assert.Equal(expected, built.Success);
        if (expected) { Assert.Equal(sample.SampleRequestId, Assert.Single(built.Data!).SampleRequestId); Assert.Equal(1234, built.Data![0].UnitPrice); }
        Assert.Empty(crmStore.Added);
    }

    private sealed class Fixture
    {
        public ProductionOrderCreationTests.TestUser User { get; } = new();
        public Guid Company => User.CompanyId!.Value;
        public ICustomerSampleSharingDbContext Db { get; }
        public SharingStore Store { get; }
        public Customer A { get; }
        public Customer B { get; }
        public CustomerSampleSharingGroup Group { get; }
        public CustomerSampleSharingMember MemberA { get; }
        public CustomerSampleSharingMember MemberB { get; }
        public ICustomerVisibilityService Visibility { get; }
        public IOptions<CustomerSampleSharingOptions> Options { get; } = Microsoft.Extensions.Options.Options.Create(new CustomerSampleSharingOptions { Enabled = true });
        public CustomerSampleSharingService Service => new(Db, Visibility, Options);
        public Fixture()
        {
            User.Roles = ["Developer"];
            Db = DispatchProxy.Create<ICustomerSampleSharingDbContext, SharingStore>(); Store = (SharingStore)(object)Db;
            A = new() { CustomerId = Guid.NewGuid(), CompanyId = Company, IsActive = true, ExternalId = "A", CustomerName = "Ordering" };
            B = new() { CustomerId = Guid.NewGuid(), CompanyId = Company, IsActive = true, ExternalId = "B", CustomerName = "Sample owner" };
            Group = new() { CompanyId = Company, Name = "Shared" };
            MemberA = new() { CompanyId = Company, CustomerId = A.CustomerId, Customer = A, GroupId = Group.Id, Group = Group };
            MemberB = new() { CompanyId = Company, CustomerId = B.CustomerId, Customer = B, GroupId = Group.Id, Group = Group };
            Store.Seed(nameof(Db.Customers), A, B); Store.Seed(nameof(Db.CustomerSampleSharingGroups), Group);
            Store.Seed(nameof(Db.CustomerSampleSharingMembers), MemberA, MemberB);
            Visibility = DispatchProxy.Create<ICustomerVisibilityService, VisibilityProxy>();
            var proxy = (VisibilityProxy)(object)Visibility;
            proxy.CustomerId = A.CustomerId;
            proxy.Scope = new ViewerScope(Company, User.EmployeeId!.Value, false, false, new HashSet<Guid>(), new HashSet<Guid>(), Clock.NowValue);
        }
    }
    public class SharingStore : ProductionOrderTestStore
    {
        public int? FailAtSave { get; set; }
        public int Locks { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "SaveChangesAsync" && Saves + 1 == FailAtSave) throw new InvalidOperationException("Simulated batch failure");
            if (method!.Name == "LockCustomerSampleSharingAsync") { Locks++; return Task.CompletedTask; }
            if (method.Name == "BeginCustomerSampleSharingTransactionAsync") return Task.FromResult<IDbContextTransaction>(Transaction);
            return base.Invoke(method, args);
        }
    }
    public class VisibilityProxy : DispatchProxy
    {
        public bool AllCustomers { get; set; }
        public Guid CustomerId { get; set; }
        public ViewerScope Scope { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "BuildReadScopeAsync" or "BuildScopeAsync" => Task.FromResult(Scope),
            "ApplyCustomerVisibility" => AllCustomers ? (IQueryable<Customer>)args![0]! : ((IQueryable<Customer>)args![0]!).Where(x => x.CustomerId == CustomerId),
            "ApplySampleRequestVisibility" => new CustomerVisibilityService(null!, null!, null!).ApplySampleRequestVisibility(
                (IQueryable<SampleRequest>)args![0]!, (IQueryable<Customer>)args[1]!, (ViewerScope)args[2]!),
            _ => throw new NotSupportedException(method.Name)
        };
    }
    private sealed class Clock : IDateTimeProvider { public static readonly DateTime NowValue = new(2026, 11, 17, 10, 0, 0); public DateTime Now => NowValue; }
}
