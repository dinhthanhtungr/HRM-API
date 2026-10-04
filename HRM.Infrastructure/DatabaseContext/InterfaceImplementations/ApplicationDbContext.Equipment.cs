using HRM.Application.Abstractions.Persistence.MRO;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Entities.MROSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : IEquipmentDbContext
{
    public async Task<bool> DeleteEquipmentAsync(EquipmentMRO equipment, CancellationToken cancellationToken)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        // Lock the parent before checking history: concurrent assessment inserts need the same FK row lock.
        var locked = await Database.SqlQuery<int>($"SELECT equipment_id AS \"Value\" FROM mro.equipment WHERE equipment_id = {equipment.EquipmentId} AND factory_id = {equipment.FactoryId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (locked.Count == 0) return false;
        var hasAssessment = await Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM mro.equipment_assessment_hdr WHERE equipment_id = {equipment.EquipmentId}")
            .AnyAsync(cancellationToken);
        if (hasAssessment) return false;
        EquipmentsMro.Remove(equipment);
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
