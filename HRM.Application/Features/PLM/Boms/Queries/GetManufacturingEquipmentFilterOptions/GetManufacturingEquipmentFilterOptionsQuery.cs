using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingEquipmentFilterOptions;

public sealed record GetManufacturingEquipmentFilterOptionsQuery
    : IRequest<ManufacturingEquipmentFilterOptionsDto>;
