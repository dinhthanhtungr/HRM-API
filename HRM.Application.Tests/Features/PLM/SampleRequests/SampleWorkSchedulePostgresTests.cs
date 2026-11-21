using System.Text.Json;
using System.Reflection;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Commands.UpdateSampleWorkSchedule;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

/// <summary>Isolated PostgreSQL verifies atomic date/audit persistence and reads the TP pause gate.</summary>
public sealed class SampleWorkSchedulePostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task DatesAndAuditAreScopedVersionedAndNeverModifyWorkflow()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var type in new[] { typeof(SampleRequest), typeof(Employee), typeof(AuditLog), typeof(Customer), typeof(SampleRequestWorkflowEvent) })
        {
            var entity = db.Model.FindEntityType(type)!;
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var columns = entity.GetProperties().Select(p =>
            {
                var defaultSql = p.TryGetDefaultValue(out var value) && value is not null
                    ? " DEFAULT " + p.GetRelationalTypeMapping().GenerateSqlLiteral(value) : "";
                return $"\"{p.GetColumnName(table)}\" {p.GetRelationalTypeMapping().StoreType} NULL{defaultSql}";
            });
            var key = entity.FindPrimaryKey()!.Properties.Single().GetColumnName(table);
            // Identifiers and types are trusted EF metadata, only inside the disposable test cluster.
            await using var create = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{table.Schema}\"; CREATE TABLE \"{table.Schema}\".\"{table.Name}\" ({string.Join(',', columns)}, PRIMARY KEY (\"{key}\"));", connection);
            await create.ExecuteNonQueryAsync();
        }
        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser");
        var oldToken = new DateTime(2026, 11, 3, 8, 0, 0);
        var clock = new Clock();
        var sample = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = Guid.NewGuid(), Status = "InProgress", IsActive = true, UpdatedDate = oldToken,
            FormulaId = Guid.NewGuid(), ExpectedDeliveryDate = new(2026, 11, 8), RealPriceQuoteDate = new(2026, 11, 2) };
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, ExternalId = "KH_TEST", IsActive = true };
        sample.CustomerId = customer.CustomerId;
        db.Customers.Add(customer);
        db.SampleRequests.Add(sample);
        db.Employees.Add(new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId.Value, IsActive = true });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        UpdateSampleWorkScheduleCommandHandler Handler(SampleTrialFeedbackCorrectionTests.User current) => new(db, current,
            new CurrentUserPermissionService(current), RestrictedVisibility.Create(current), clock);
        var command = new UpdateSampleWorkScheduleCommand { SampleRequestId = sample.SampleRequestId,
            ExpectedUpdatedDate = oldToken, ExpectedPriceQuoteDate = new(2026, 11, 6), ClearFields = ["expectedDeliveryDate"] };
        Assert.True((await Handler(user).Handle(command, default)).Success);
        var actual = await db.SampleRequests.AsNoTracking().SingleAsync();
        Assert.Null(actual.ExpectedDeliveryDate);
        Assert.Equal(new DateTime(2026, 11, 6), actual.ExpectedPriceQuoteDate);
        Assert.Equal("InProgress", actual.Status);
        Assert.Equal(sample.FormulaId, actual.FormulaId);
        Assert.Equal(sample.RealPriceQuoteDate, actual.RealPriceQuoteDate);
        Assert.Equal(clock.Now, actual.UpdatedDate);
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(SampleWorkScheduleRules.AuditReason, audit.Reason);
        Assert.Equal(user.EmployeeId, audit.ChangedBy);
        Assert.Equal(JsonValueKind.Null, audit.NewValues!.RootElement.GetProperty("ExpectedDeliveryDate").ValueKind);
        Assert.Equal("2026-11-08T00:00:00", audit.OldValues!.RootElement.GetProperty("ExpectedDeliveryDate").GetString());
        Assert.False((await Handler(user).Handle(command, default)).Success); // stale token, no second audit
        Assert.Equal(1, await db.AuditLogs.CountAsync());

        var otherCompany = new SampleTrialFeedbackCorrectionTests.User("LabAdmin");
        db.Employees.Add(new Employee { EmployeeId = otherCompany.EmployeeId!.Value, CompanyId = otherCompany.CompanyId!.Value, IsActive = true });
        await db.SaveChangesAsync();
        command.ExpectedUpdatedDate = actual.UpdatedDate;
        Assert.True((await Handler(user).Handle(command, default)).Success); // repeated dates are a no-op
        Assert.Equal(1, await db.AuditLogs.CountAsync());
        Assert.False((await Handler(otherCompany).Handle(command, default)).Success);
        await db.Customers.ExecuteUpdateAsync(set => set.SetProperty(x => x.ExternalId, "KH_VIETAUS"));
        Assert.False((await Handler(user).Handle(command, default)).Success);
        await db.Customers.ExecuteUpdateAsync(set => set.SetProperty(x => x.ExternalId, "KH_TEST"));
        await db.SampleRequests.ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "Completed"));
        Assert.False((await Handler(user).Handle(command, default)).Success);
        await db.SampleRequests.ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "Cancelled"));
        Assert.False((await Handler(user).Handle(command, default)).Success);
        await db.SampleRequests.ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "InProgress").SetProperty(x => x.IsActive, false));
        Assert.False((await Handler(user).Handle(command, default)).Success);
        await db.SampleRequests.ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, true));
        await db.Employees.Where(x => x.EmployeeId == user.EmployeeId).ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false));
        Assert.False((await Handler(user).Handle(command, default)).Success);
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 3, 9, 0, 0); }

    public class RestrictedVisibility : DispatchProxy
    {
        private SampleTrialFeedbackCorrectionTests.User _user = null!;
        private CustomerVisibilityService _real = null!;
        public static ICustomerVisibilityService Create(SampleTrialFeedbackCorrectionTests.User user)
        {
            var proxy = Create<ICustomerVisibilityService, RestrictedVisibility>();
            ((RestrictedVisibility)proxy)._user = user;
            ((RestrictedVisibility)proxy)._real = new CustomerVisibilityService(null!, user, new Clock());
            return proxy;
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "BuildScopeAsync" or "BuildReadScopeAsync" => Task.FromResult(new ViewerScope(_user.CompanyId!.Value,
                _user.EmployeeId!.Value, _user.IsInRole("LabUser"), false, new HashSet<Guid>(), new HashSet<Guid>(), DateTime.Now)),
            "ApplySampleRequestVisibility" => _real.ApplySampleRequestVisibility((IQueryable<SampleRequest>)args![0]!,
                (IQueryable<Customer>)args[1]!, (ViewerScope)args[2]!),
            _ => throw new NotSupportedException(method.Name)
        };
    }
}
