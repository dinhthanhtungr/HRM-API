using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.CustomerCare;

public sealed class CustomerTaxCodeTeamConflictTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 10, 0, 0);

    [Theory]
    [InlineData("same-team", true)]
    [InlineData("different-team", false)]
    [InlineData("official", true)]
    [InlineData("foreign-company", false)]
    [InlineData("inactive", false)]
    [InlineData("expired-claim", false)]
    [InlineData("inactive-claim", false)]
    [InlineData("internal-customer", false)]
    [InlineData("different-tax", false)]
    [InlineData("same-team-assignment", true)]
    public async Task Create_UsesExactTeamAndPreservesOfficialCompanyRule(string scenario, bool blocked)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<ICRMReadDbContext>();
        var company = Guid.NewGuid();
        var team = Guid.NewGuid();
        var existing = Customer(company, team);
        if (scenario == "different-team") existing.CustomerClaims.Single().GroupId = Guid.NewGuid();
        if (scenario == "official") { existing.IsLead = false; existing.CustomerClaims.Clear(); }
        if (scenario == "foreign-company") existing.CompanyId = Guid.NewGuid();
        if (scenario == "inactive") existing.IsActive = false;
        if (scenario == "expired-claim") existing.CustomerClaims.Single().ExpiresAt = Now.AddSeconds(-1);
        if (scenario == "inactive-claim") existing.CustomerClaims.Single().IsActive = false;
        if (scenario == "internal-customer") existing.ExternalId = "KH_VIETAUS";
        if (scenario == "different-tax") existing.TaxNumber = "9999999999";
        if (scenario == "same-team-assignment")
        {
            existing.CustomerClaims.Clear();
            existing.CustomerAssignments.Add(new CustomerAssignment { GroupId = team, CompanyId = company,
                IsActive = true, Employee = new Employee { FullName = "Sales test" }, Group = new Group { Name = "CMR.R1" } });
        }
        store.Seed(nameof(db.Customers), existing);
        var result = await new CustomerTaxCodeConflictService(db, new Clock())
            .FindBlockingConflictAsync(company, " 0319-530-072 ", null, default, team);
        Assert.Equal(blocked, result is not null);
        if (result is not null)
        {
            Assert.Equal(existing.CustomerId, result.CustomerId);
            Assert.Equal(existing.IsLead, result.IsLead);
            Assert.Contains(existing.IsLead ? "cùng nhóm" : "chinh thuc",
                CustomerTaxCodeConflictService.BuildConflictMessage("0319530072", result));
            if (existing.IsLead)
            {
                var message = CustomerTaxCodeConflictService.BuildConflictMessage("0319530072", result);
                Assert.Contains(existing.ExternalId, message);
                Assert.Contains(existing.CustomerName, message);
                Assert.Contains("Sale phụ trách: Sales test", message);
                Assert.Contains("Team đang giữ: CMR.R1", message);
            }
        }
        Assert.Equal(0, store.Saves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Update_UsesRecordTeamAndExcludesSelf(bool sameTeam)
    {
        var (db, _) = ProductionOrderTestStore.CreateContext<ICRMReadDbContext>();
        var company = Guid.NewGuid();
        var team = Guid.NewGuid();
        var target = Customer(company, team);
        var existing = Customer(company, sameTeam ? team : Guid.NewGuid());
        ((ProductionOrderTestStore)(object)db).Seed(nameof(db.Customers), target, existing);
        var service = new CustomerTaxCodeConflictService(db, new Clock());
        var result = await service.FindBlockingConflictAsync(company, "0319530072", target.CustomerId, default);
        Assert.Equal(sameTeam, result is not null);
        if (result is not null) Assert.Equal(existing.CustomerId, result.CustomerId);
    }

    [Fact]
    public async Task SelfAndEmptyTax_DoNotConflict()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<ICRMReadDbContext>();
        var target = Customer(Guid.NewGuid(), Guid.NewGuid());
        store.Seed(nameof(db.Customers), target);
        var service = new CustomerTaxCodeConflictService(db, new Clock());
        Assert.Null(await service.FindBlockingConflictAsync(target.CompanyId, target.TaxNumber, target.CustomerId, default));
        Assert.Null(await service.FindBlockingConflictAsync(target.CompanyId, " - ", null, default));
    }

    [Fact]
    public async Task OfficialTarget_DoesNotGainLeadRestriction()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<ICRMReadDbContext>();
        var company = Guid.NewGuid();
        var team = Guid.NewGuid();
        var target = Customer(company, team);
        target.IsLead = false;
        store.Seed(nameof(db.Customers), target, Customer(company, team));
        Assert.Null(await new CustomerTaxCodeConflictService(db, new Clock())
            .FindBlockingConflictAsync(company, target.TaxNumber, target.CustomerId, default));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", " ")]
    public void LeadMessage_ExplicitlyShowsMissingSaleAndTeam(string? sale, string? team)
    {
        var conflict = new CustomerTaxCodeConflict(Guid.NewGuid(), "KH_TEST", "Test customer",
            "0319530072", null, sale, null, team, IsLead: true);
        var message = CustomerTaxCodeConflictService.BuildConflictMessage("0319530072", conflict);
        Assert.Contains("KH_TEST", message);
        Assert.Contains("Test customer", message);
        Assert.Contains("Sale phụ trách: chưa xác định", message);
        Assert.Contains("Team đang giữ: chưa xác định", message);
    }

    private static Customer Customer(Guid company, Guid team)
    {
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            IsLead = true, ExternalId = "KH_TEST", CustomerName = "Test customer", TaxNumber = "0319530072" };
        customer.CustomerClaims.Add(new CustomerClaim { GroupId = team, CompanyId = company, IsActive = true,
            Type = ClaimType.Work, ExpiresAt = Now.AddDays(1), Employee = new Employee { FullName = "Sales test" },
            Group = new Group { Name = "CMR.R1" } });
        return customer;
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => CustomerTaxCodeTeamConflictTests.Now; }
}
