using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestUserPreferencePersistenceTests
{
    [Fact]
    public void FullModel_MapsPersonalPinsWithoutShadowForeignKeysOrCascadeDeletes()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var entity = db.Model.FindEntityType(typeof(SampleRequestUserPreference))!;
        Assert.Equal("SampleRequests", entity.GetSchema());
        Assert.Equal("SampleRequestUserPreferences", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "EmployeeId", "SampleRequestId" }));
        Assert.Equal(3, entity.GetForeignKeys().Count());
        Assert.All(entity.GetForeignKeys(), fk =>
        {
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
            Assert.All(fk.Properties, property => Assert.False(property.IsShadowProperty()));
        });

        var companyId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var sql = db.SampleRequestUserPreferences
            .Where(p => p.CompanyId == companyId && p.EmployeeId == employeeId && p.IsPinned)
            .OrderByDescending(p => p.PinnedAt).Select(p => p.SampleRequestId).ToQueryString();
        Assert.Contains("\"SampleRequests\".\"SampleRequestUserPreferences\"", sql);
        var preference = new SampleRequestUserPreference
        {
            CompanyId = companyId, EmployeeId = employeeId, SampleRequestId = Guid.NewGuid(),
            CreatedDate = new DateTime(2026, 11, 7), IsPinned = true
        };
        db.SampleRequestUserPreferences.Add(preference);
        Assert.Equal(EntityState.Added, db.Entry(preference).State);
        Assert.Single(db.ChangeTracker.Entries());
    }
}
