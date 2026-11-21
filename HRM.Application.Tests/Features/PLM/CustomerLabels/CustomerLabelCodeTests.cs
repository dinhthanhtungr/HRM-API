using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.CustomerLabels.Commands.CreateCustomerLabel;
using HRM.Application.Features.PLM.CustomerLabels.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Application.Tests.Features.PLM.SampleRequests;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.PrintectSchema;

namespace HRM.Application.Tests.Features.PLM.CustomerLabels;

public sealed class CustomerLabelCodeTests
{
    [Fact]
    public async Task TemporaryNumbersAreCustomerScopedAndSurviveReplacementWithOfficialCode()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, ExternalId = "KH_375", IsActive = true };
        var other = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, ExternalId = "KH_376", IsActive = true };
        var first = new Product { ProductId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, IsActive = true };
        var second = new Product { ProductId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, IsActive = true };
        store.Seed(nameof(db.Customers), customer, other);
        store.Seed(nameof(db.Products), first, second);
        var codes = new Codes();
        var handler = new CreateCustomerLabelCommandHandler(db, user, new Clock(), codes);
        async Task<CustomerLabelHeader> Create(Product product, Customer c)
        {
            var result = await handler.Handle(new() { ProductId = product.ProductId, CustomerId = c.CustomerId,
                ColorCode = "STALE_CLIENT_CODE", CustomerExternalId = "SPOOFED",
                Details = [new() { LineNo = 1, FieldKey = "Name", FieldValue = "Sale label text" }] }, default);
            Assert.True(result.Success, result.Message);
            return store.Added.OfType<CustomerLabelHeader>().Last();
        }
        var label = await Create(first, customer);
        Assert.Equal("KH_375_1", label.ColorCode);
        Assert.Equal("KH_375", label.CustomerExternalId);
        label.Product = first; label.Customer = customer;
        store.Seed(nameof(db.CustomerLabelHeaders), label);
        first.ColourCode = "NEW123";
        await new CustomerLabelColorCodeSyncService(db).SyncAsync(first.ProductId, user.CompanyId.Value, first.ColourCode, default);
        Assert.Equal("NEW123", label.ColorCode);
        Assert.Equal("Sale label text", label.Details.Single().FieldValue);
        Assert.Equal("KH_375_2", (await Create(second, customer)).ColorCode);
        Assert.Equal("KH_376_1", (await Create(second, other)).ColorCode);
        Assert.Null(second.ColourCode);
        var official = await Create(first, customer);
        Assert.Equal("NEW123", official.ColorCode);
        Assert.Equal(3, codes.Allocations);
    }

    [Theory]
    [InlineData("product")] [InlineData("customer")] [InlineData("inactive")]
    public async Task InvalidScopeCannotAllocateOrCreate(string invalid)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = invalid == "product" ? Guid.NewGuid() : user.CompanyId!.Value, IsActive = true };
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = invalid == "customer" ? Guid.NewGuid() : user.CompanyId!.Value, ExternalId = "KH_1", IsActive = invalid != "inactive" };
        store.Seed(nameof(db.Products), product); store.Seed(nameof(db.Customers), customer);
        var codes = new Codes();
        var result = await new CreateCustomerLabelCommandHandler(db, user, new Clock(), codes).Handle(new() { ProductId = product.ProductId, CustomerId = customer.CustomerId }, default);
        Assert.False(result.Success); Assert.Equal(0, codes.Allocations); Assert.Empty(store.Added);
    }

    [Fact]
    public async Task CodeSyncAndProjectionPreserveDetailsAndRespectBothCompanyParents()
    {
        var company = Guid.NewGuid(); var productId = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        CustomerLabelHeader Label(Guid customerCompany) => new() { Id = Guid.NewGuid(), ProductId = productId,
            Product = new() { ProductId = productId, CompanyId = company, ColourCode = " OFFICIAL " },
            Customer = new() { CompanyId = customerCompany }, ColorCode = "KH_1_1",
            Details = [new() { LineNo = 1, FieldKey = "Name", FieldValue = "Keep me" }] };
        var own = Label(company); var foreign = Label(Guid.NewGuid());
        store.Seed(nameof(db.CustomerLabelHeaders), own, foreign);
        Assert.Equal("OFFICIAL", CustomerLabelProjection.ToDto.Compile()(own).ColorCode);
        await new CustomerLabelColorCodeSyncService(db).SyncAsync(productId, company, "NEW456", default);
        Assert.Equal("NEW456", own.ColorCode); Assert.Equal("KH_1_1", foreign.ColorCode);
        Assert.Null(own.UpdatedDate);
        Assert.Equal("Keep me", own.Details.Single().FieldValue); Assert.Equal(0, store.Saves);
        own.Product.ColourCode = " "; Assert.Equal("NEW456", CustomerLabelProjection.ToDto.Compile()(own).ColorCode);
    }

    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 17, 10, 0, 0); }
    private sealed class Codes : IExternalIdService
    {
        private readonly Dictionary<string, int> numbers = [];
        public int Allocations { get; private set; }
        public Task<string> GenerateGlobalCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default)
        {
            numbers.TryGetValue(prefix, out var number); numbers[prefix] = ++number; Allocations++;
            return Task.FromResult(prefix + "_" + number);
        }
        public Task<string> GenerateMonthlyCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

