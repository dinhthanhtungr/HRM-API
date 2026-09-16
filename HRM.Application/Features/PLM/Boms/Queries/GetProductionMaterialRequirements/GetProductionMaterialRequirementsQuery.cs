using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetProductionMaterialRequirements;

public sealed record GetProductionMaterialRequirementsQuery(Guid MfgProductionOrderId)
    : IRequest<OperationResult<IReadOnlyList<ProductionMaterialRequirementDto>>>;
