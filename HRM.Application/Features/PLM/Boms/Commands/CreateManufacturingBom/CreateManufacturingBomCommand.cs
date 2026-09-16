using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingBom;

public sealed record CreateManufacturingBomCommand(
    Guid EngineeringBomVersionId,
    CreateManufacturingBomRequest Request)
    : IRequest<OperationResult<BomVersionDto>>;
