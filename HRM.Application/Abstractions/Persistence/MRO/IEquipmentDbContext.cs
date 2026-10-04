using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MROSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.MRO;

public interface IEquipmentDbContext
{
    DbSet<EquipmentMRO> EquipmentsMro { get; }
    DbSet<EquipmentDetailMRO> EquipmentDetailsMro { get; }
    DbSet<EquipmentSpecMRO> EquipmentSpecsMro { get; }
    DbSet<EquipmentTypeMRO> EquipmentTypesMro { get; }
    DbSet<AreaMRO> AreasMro { get; }
    DbSet<Company> Companies { get; }
    DbSet<Part> Parts { get; }
    Task<bool> DeleteEquipmentAsync(EquipmentMRO equipment, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
