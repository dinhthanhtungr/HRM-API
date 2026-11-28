using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Queries.GetEmployeeDropdown;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Application.Tests.Features.Employees;

public sealed class EmployeeLookupPagingTests
{
    [Fact]
    public async Task EqualNamesHaveStablePagingAndKeepCompanyAndActiveScope()
    {
        var (db, store) = IqcTestStore.CreateContext<IEmployeeReadDbContext>();
        var company = Guid.NewGuid();
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        Employee Row(Guid id, Guid owner, bool active = true) => new() {
            EmployeeId = id, CompanyId = owner, IsActive = active, Status = "Active",
            FullName = "Same name", ExternalId = "EMP"
        };
        store.Seed(nameof(db.Employees), Row(second, company), Row(first, company),
            Row(Guid.NewGuid(), Guid.NewGuid()), Row(Guid.NewGuid(), company, false));
        var handler = new GetEmployeeLookupQueryHandler(db, new User(company));
        using var timing = new HRM.Application.Commons.Diagnostics.RequestTiming();
        var page = await handler.Handle(new GetEmployeeLookupQuery { PageSize = 1 }, default);
        var next = await handler.Handle(new GetEmployeeLookupQuery { PageSize = 1, PageNumber = 2 }, default);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(first, Assert.Single(page.Items).EmployeeId);
        Assert.Equal(second, Assert.Single(next.Items).EmployeeId);
        Assert.Contains(timing.Snapshot(), step => step.Key == "step_GetEmployeeLookupQueryHandler_ToPagedResultAsync");
        Assert.Contains(timing.Snapshot(), step => step.Key == "step_PaginationExtensions_ToPagedResultAsync_totalCount_CountAsync");
        Assert.Contains(timing.Snapshot(), step => step.Key == "step_PaginationExtensions_ToPagedResultAsync_items_ToListAsync");
    }

    private sealed record User(Guid Company) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => Guid.Empty;
        public Guid? CompanyId => Company;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
