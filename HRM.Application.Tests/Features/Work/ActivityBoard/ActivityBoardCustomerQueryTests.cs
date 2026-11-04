using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Work.ActivityBoard.Dtos;
using HRM.Application.Features.Work.ActivityBoard.Queries.GetWorkActivityBoardCustomer;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.WorkTaskEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Work.ActivityBoard;

public sealed class ActivityBoardCustomerQueryTests
{
    [Fact]
    public void ActivitySetsPreserveVisibilityFiltersAndOpenTaskPriority()
    {
        var company = Guid.NewGuid();
        var employee = Guid.NewGuid();
        var other = Guid.NewGuid();
        var group = Guid.NewGuid();
        var now = new DateTime(2026, 10, 5);
        var customers = Enumerable.Range(0, 6).Select(i => new Customer
        {
            CustomerId = Guid.NewGuid(), CompanyId = company, IsLead = i != 0,
            CustomerName = i.ToString(), ExternalId = i.ToString()
        }).ToArray();
        customers[2].CustomerClaims.Add(new CustomerClaim
        {
            IsActive = true, Type = ClaimType.Work, ExpiresAt = now.AddDays(1), GroupId = group
        });
        customers[3].CustomerClaims.Add(new CustomerClaim
        {
            IsActive = true, Type = ClaimType.Work, ExpiresAt = now.AddDays(-1), GroupId = group
        });
        var interactions = customers.Select((c, i) => new CustomerInteraction
        {
            Id = Guid.NewGuid(), CustomerId = c.CustomerId, CompanyId = company,
            CreatedBy = other, AssignedSaleEmployeeId = i == 4 ? employee : null, IsActive = i != 5
        }).ToArray();
        var tasks = customers.Select((c, i) => new WorkTask
        {
            Id = Guid.NewGuid(), CompanyId = i == 5 ? Guid.NewGuid() : company,
            CreatedBy = i == 1 ? employee : other, IsActive = i != 4,
            Status = i == 3 ? WorkTaskStatus.Done : WorkTaskStatus.Pending,
            Assignees = i == 4 ? [new WorkTaskAssignee { EmployeeId = employee, IsActive = true }] : []
        }).ToArray();
        var references = tasks.Select((t, i) => new WorkTaskReference
        {
            WorkTaskId = t.Id, ReferenceType = i % 2 == 0 ? WorkReferenceType.Customer : WorkReferenceType.CustomerInteraction,
            ReferenceId = i % 2 == 0 ? customers[i].CustomerId : interactions[i].Id
        }).ToArray();
        var plans = customers.Select((c, i) => new WorkPlan
        {
            Id = Guid.NewGuid(), CompanyId = company, CreatedBy = other,
            AssignedToEmployeeId = i == 3 ? employee : null,
            IsActive = i != 4, Status = i == 3 ? WorkPlanStatus.Completed : WorkPlanStatus.Active
        }).ToArray();
        var planReferences = plans.Select((p, i) => new WorkPlanReference
        {
            WorkPlanId = p.Id, ReferenceType = WorkReferenceType.Customer, ReferenceId = customers[i].CustomerId
        }).ToArray();
        var db = DispatchProxy.Create<ICRMReadDbContext, MemoryContext>();
        var store = (MemoryContext)(object)db;
        store.Values[nameof(db.WorkTasks)] = new MemorySet<WorkTask>(tasks);
        store.Values[nameof(db.WorkTaskReferences)] = new MemorySet<WorkTaskReference>(references.Concat(references));
        store.Values[nameof(db.WorkPlans)] = new MemorySet<WorkPlan>(plans);
        store.Values[nameof(db.WorkPlanReferences)] = new MemorySet<WorkPlanReference>(planReferences);
        store.Values[nameof(db.CustomerInteractions)] = new MemorySet<CustomerInteraction>(interactions);

        for (var mask = 0; mask < 128; mask++)
        {
            var request = new WorkActivityBoardCustomerListQueryDto
            {
                OnlyMine = (mask & 1) != 0, IncludeCompleted = (mask & 2) != 0, IncludeInactive = (mask & 4) != 0
            };
            var scope = new ViewerScope(company, employee, (mask & 8) != 0, false, new HashSet<Guid> { group },
                new HashSet<Guid> { employee }, now);
            var withTasks = (mask & 16) != 0;
            var withPlans = (mask & 32) != 0;
            var withInteractions = (mask & 64) != 0;
            var rows = GetWorkActivityBoardCustomersQueryHandler.BuildCustomerRows(
                db, customers.AsQueryable(), scope, request, withTasks, withPlans, withInteractions).ToArray();
            foreach (var row in rows)
            {
                var c = customers.Single(c => c.CustomerId == row.CustomerId);
                var broad = !c.IsLead || scope.HasFullCustomerView || c.CustomerClaims.Any(cl =>
                    cl.IsActive && cl.Type == ClaimType.Work && cl.ExpiresAt > now && cl.GroupId == group);
                var visibleTasks = tasks.Where(t => withTasks && t.CompanyId == company &&
                    (request.IncludeInactive || t.IsActive) &&
                    references.Any(r => r.WorkTaskId == t.Id &&
                        (r.ReferenceType == WorkReferenceType.Customer && r.ReferenceId == c.CustomerId ||
                         r.ReferenceType == WorkReferenceType.CustomerInteraction &&
                         interactions.Any(i => i.CompanyId == company && i.Id == r.ReferenceId && i.CustomerId == c.CustomerId))) &&
                    (broad && !request.OnlyMine || t.CreatedBy == employee || t.AssignedToEmployeeId == employee ||
                     t.Assignees.Any(a => a.EmployeeId == employee && a.IsActive))).ToArray();
                bool Open(WorkTask t) => t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Canceled;
                var hasPlan = plans.Any(p => withPlans && p.CompanyId == company &&
                    (request.IncludeInactive || p.IsActive) &&
                    (request.IncludeCompleted || p.Status != WorkPlanStatus.Completed && p.Status != WorkPlanStatus.Canceled) &&
                    planReferences.Any(r => r.WorkPlanId == p.Id && r.ReferenceType == WorkReferenceType.Customer && r.ReferenceId == c.CustomerId) &&
                    (broad && !request.OnlyMine || p.CreatedBy == employee || p.AssignedToEmployeeId == employee ||
                     p.Assignees.Any(a => a.EmployeeId == employee && a.IsActive)));
                var hasInteraction = interactions.Any(i => withInteractions && i.CompanyId == company &&
                    i.CustomerId == c.CustomerId && (request.IncludeInactive || i.IsActive) &&
                    (broad && !request.OnlyMine || i.CreatedBy == employee || i.AssignedSaleEmployeeId == employee));
                Assert.Equal(visibleTasks.Any(Open), row.HasOpenTask);
                Assert.Equal(visibleTasks.Any(t => request.IncludeCompleted || Open(t)) || hasPlan || hasInteraction, row.HasActivity);
            }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CountAndPageQueriesTranslateToPostgres(bool onlyMine, bool fullView)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var employee = Guid.NewGuid();
        var scope = new ViewerScope(company, employee, fullView, false, new HashSet<Guid> { Guid.NewGuid() },
            new HashSet<Guid> { employee }, DateTime.UtcNow);
        var rows = GetWorkActivityBoardCustomersQueryHandler.BuildCustomerRows(db,
            db.Customers.Where(c => c.CompanyId == company && c.IsActive == true),
            scope, new WorkActivityBoardCustomerListQueryDto { OnlyMine = onlyMine }, true, true, true)
            .Where(c => c.HasActivity);
        var countSql = rows.GroupBy(c => 1).Select(g => g.Count()).ToQueryString();
        var pageSql = rows.OrderByDescending(c => c.HasOpenTask).ThenBy(c => c.CustomerName)
            .ThenBy(c => c.ExternalId).Skip(15).Take(15).ToQueryString();
        Assert.Contains("UNION ALL", countSql);
        Assert.Contains("JOIN", countSql);
        Assert.Contains("LIMIT", pageSql);
        Assert.Contains("OFFSET", pageSql);
        Assert.Contains("\"CompanyId\"", countSql);
    }

    public class MemoryContext : DispatchProxy
    {
        public Dictionary<string, object> Values { get; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => Values[targetMethod!.Name[4..]];
    }

    public class MemorySet<T>(IEnumerable<T> items) : DbSet<T>, IQueryable<T> where T : class
    {
        public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType => throw new NotSupportedException();
        private readonly IQueryable<T> _query = items.AsQueryable();
        Type IQueryable.ElementType => typeof(T);
        Expression IQueryable.Expression => _query.Expression;
        IQueryProvider IQueryable.Provider => _query.Provider;
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _query.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _query.GetEnumerator();
    }
}
