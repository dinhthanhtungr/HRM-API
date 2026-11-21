using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.Tracking;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleProductionTrackingTests
{
    [Fact]
    public void TrackingListAndDetailTranslateWithVisibleSampleJoin()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User();
        var query = new SampleProductionTrackingReader(db, user, new CurrentUserPermissionService(user)).Read()!;
        var list = query.OrderByDescending(row => row.CreatedDate).ThenBy(row => row.Id).Take(25).ToQueryString();
        var detail = query.Where(row => row.Id == Guid.Empty).Take(1).ToQueryString();
        foreach (var sql in new[] { list, detail })
        {
            Assert.Contains("JOIN", sql);
            Assert.Contains("LIMIT", sql);
            Assert.DoesNotContain("num_of_batches", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FormulaMaterialSnapshots", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = [ApplicationRoles.InternalSystems.IMSUser];
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
