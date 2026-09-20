using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.ObsoleteManufacturingLossProfile;

public sealed record ObsoleteManufacturingLossProfileCommand(Guid ProfileId)
    : IRequest<OperationResult<ManufacturingLossProfileDto>>;
