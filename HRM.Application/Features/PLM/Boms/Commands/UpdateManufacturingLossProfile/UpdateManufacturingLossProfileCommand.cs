using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.UpdateManufacturingLossProfile;

public sealed record UpdateManufacturingLossProfileCommand(
    Guid ProfileId,
    UpsertManufacturingLossProfileRequest Request)
    : IRequest<OperationResult<ManufacturingLossProfileDto>>;
