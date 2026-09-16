using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.PatchManufacturingLossType;

public sealed record PatchManufacturingLossTypeCommand(
    Guid ManufacturingLossTypeId,
    PatchManufacturingLossTypeRequest Request)
    : IRequest<OperationResult<ManufacturingLossTypeDto>>;
