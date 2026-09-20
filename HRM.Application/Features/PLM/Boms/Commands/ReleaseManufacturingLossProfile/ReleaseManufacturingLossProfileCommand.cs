using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.ReleaseManufacturingLossProfile;

public sealed record ReleaseManufacturingLossProfileCommand(Guid ProfileId)
    : IRequest<OperationResult<ManufacturingLossProfileDto>>;
