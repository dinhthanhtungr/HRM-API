using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Entities.BomSchema;

namespace HRM.Application.Features.PLM.Boms.Mappers;

internal static class ManufacturingLossTypeMapper
{
    internal static ManufacturingLossTypeDto ToDto(ManufacturingLossType entity) => new()
    {
        ManufacturingLossTypeId = entity.ManufacturingLossTypeId,
        Code = entity.ExternalId,
        Name = entity.Name,
        Description = entity.Description,
        DefaultCalculationMethod = entity.DefaultCalculationMethod,
        IsRecoverable = entity.IsRecoverable,
        IsActive = entity.IsActive
    };
}
