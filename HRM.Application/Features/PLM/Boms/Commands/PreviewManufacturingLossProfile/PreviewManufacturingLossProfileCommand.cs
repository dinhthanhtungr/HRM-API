using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.PreviewManufacturingLossProfile;

public sealed record PreviewManufacturingLossProfileCommand(Guid BomVersionId, Guid ProfileId)
    : IRequest<OperationResult<ManufacturingLossProfileApplicationDto>>;
