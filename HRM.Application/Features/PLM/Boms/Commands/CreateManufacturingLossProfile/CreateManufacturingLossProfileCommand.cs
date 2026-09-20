using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingLossProfile;

public sealed record CreateManufacturingLossProfileCommand(UpsertManufacturingLossProfileRequest Request)
    : IRequest<OperationResult<ManufacturingLossProfileDto>>;
