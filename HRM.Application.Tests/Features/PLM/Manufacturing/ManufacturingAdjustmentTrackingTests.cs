using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingAdjustmentTrackingTests
{
    [Theory]
    [InlineData(false, EntityState.Modified)]
    [InlineData(true, EntityState.Added)]
    public void NewRangeOnSavedSlip_RequiresExplicitInsert(bool explicitlyAdd, EntityState expected)
    {
        // Production model only: no connection is opened and no database is written.
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var slip = new ManufacturingFormulaAdjustment { ManufacturingFormulaAdjustmentId = Guid.NewGuid() };
        db.Attach(slip);
        var batch = new ManufacturingFormulaAdjustmentBatch
        {
            ManufacturingFormulaAdjustmentBatchId = Guid.NewGuid(),
            ManufacturingFormulaAdjustmentId = slip.ManufacturingFormulaAdjustmentId,
            BatchNo = 2, TrialNo = 1,
            Items = [new() { ManufacturingFormulaAdjustmentItemId = Guid.NewGuid(), MaterialId = Guid.NewGuid() }]
        };
        slip.Batches.Add(batch);
        if (explicitlyAdd) db.ManufacturingFormulaAdjustmentBatches.Add(batch);
        db.ChangeTracker.DetectChanges();
        Assert.Equal(expected, db.Entry(batch).State);
        Assert.Equal(expected, db.Entry(Assert.Single(batch.Items)).State);
        Assert.Equal(EntityState.Unchanged, db.Entry(slip).State);
    }

    [Theory]
    [InlineData(false, EntityState.Modified)]
    [InlineData(true, EntityState.Added)]
    public void ReplacementItemOnRetainedBatch_RequiresExplicitInsert(bool explicitlyAdd, EntityState expected)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var batch = new ManufacturingFormulaAdjustmentBatch { ManufacturingFormulaAdjustmentBatchId = Guid.NewGuid() };
        db.Attach(batch);
        var item = new ManufacturingFormulaAdjustmentItem
        {
            ManufacturingFormulaAdjustmentItemId = Guid.NewGuid(),
            ManufacturingFormulaAdjustmentBatchId = batch.ManufacturingFormulaAdjustmentBatchId,
            MaterialId = Guid.NewGuid()
        };
        batch.Items.Add(item);
        if (explicitlyAdd) db.ManufacturingFormulaAdjustmentItems.Add(item);
        db.ChangeTracker.DetectChanges();
        Assert.Equal(expected, db.Entry(item).State);
        Assert.Equal(EntityState.Unchanged, db.Entry(batch).State);
    }
}
