using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingLossType;

public sealed record CreateManufacturingLossTypeCommand(CreateManufacturingLossTypeRequest Request)
    : IRequest<OperationResult<ManufacturingLossTypeDto>>;
