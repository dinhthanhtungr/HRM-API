using HRM.Domain.Entities.MROSchema;

namespace HRM.Application.Features.MRO.Equipment.Services;

internal static class EquipmentScope
{
    public static IQueryable<EquipmentMRO> ForCompany(IQueryable<EquipmentMRO> machines, Guid? companyId)
        => machines.Where(x => companyId != null && companyId != Guid.Empty && x.FactoryId == companyId);

    public static IQueryable<EquipmentSpecMRO> ForSpecifications(
        IQueryable<EquipmentSpecMRO> specifications, Guid? companyId, int equipmentId, int? specId = null)
        => specifications.Where(x => companyId != null && companyId != Guid.Empty
            && x.Equipment.FactoryId == companyId && x.EquipmentId == equipmentId
            && (specId == null || x.SpecId == specId));
}
