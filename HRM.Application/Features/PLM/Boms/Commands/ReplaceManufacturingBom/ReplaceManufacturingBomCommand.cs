using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.ReplaceManufacturingBom;

public sealed record ReplaceManufacturingBomCommand(
    Guid BomVersionId,
    ReplaceManufacturingBomRequest Request)
    : IRequest<OperationResult<BomVersionDto>>;
