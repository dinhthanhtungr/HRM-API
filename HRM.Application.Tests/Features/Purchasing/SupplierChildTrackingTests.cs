using HRM.Domain.Entities.MaterialSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class SupplierChildTrackingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NewChildWithAssignedGuid_IsInserted_AndExistingParentIsNotInserted(bool contact)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var parent = new Supplier { SupplierId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        db.Attach(parent);
        object child;
        if (contact)
        {
            var entity = new SupplierContact { ContactId = Guid.CreateVersion7(), SupplierId = parent.SupplierId, FirstName = "New" };
            parent.SupplierContacts.Add(entity);
            db.ChangeTracker.DetectChanges();
            await db.SupplierContacts.AddAsync(entity);
            child = entity;
        }
        else
        {
            var entity = new SupplierAddress { AddressId = Guid.CreateVersion7(), SupplierId = parent.SupplierId, AddressLine = "New" };
            parent.SupplierAddresses.Add(entity);
            db.ChangeTracker.DetectChanges();
            await db.SupplierAddresses.AddAsync(entity);
            child = entity;
        }
        db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Added, db.Entry(child).State);
        Assert.Equal(EntityState.Unchanged, db.Entry(parent).State);
        Assert.Single(db.ChangeTracker.Entries(), x => x.State == EntityState.Added);
    }
}
