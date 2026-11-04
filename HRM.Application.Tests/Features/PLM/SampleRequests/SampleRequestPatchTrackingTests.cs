using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestPatchTrackingTests
{
    [Fact]
    public void TrackedPatchPreservesDatabaseTimestampAndOnlyMarksChangedFields()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var original = new DateTime(2026, 11, 1, 16, 33, 20).AddTicks(9314030);
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), UpdatedDate = original };
        db.Attach(request); // Simulates the explicit AsTracking query snapshot; never opens a connection.
        request.UpdatedDate = original.AddMinutes(1);
        request.LabReceivedDate = original.Date;
        db.ChangeTracker.DetectChanges();

        var entry = db.Entry(request);
        Assert.True(entry.Property(x => x.UpdatedDate).Metadata.IsConcurrencyToken);
        Assert.Equal(original, entry.Property(x => x.UpdatedDate).OriginalValue);
        Assert.True(entry.Property(x => x.UpdatedDate).IsModified);
        Assert.True(entry.Property(x => x.LabReceivedDate).IsModified);
        Assert.False(entry.Property(x => x.CustomerId).IsModified);
        Assert.False(entry.Property(x => x.ProductId).IsModified);
    }

    [Fact]
    public void AttachingAlreadyMutatedDetachedPatchWouldLoseOriginalConcurrencyTimestamp()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var original = new DateTime(2026, 11, 1, 16, 33, 20);
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), UpdatedDate = original };
        request.UpdatedDate = original.AddMinutes(1);
        db.SampleRequests.Update(request);
        Assert.NotEqual(original, db.Entry(request).Property(x => x.UpdatedDate).OriginalValue);
    }
}
