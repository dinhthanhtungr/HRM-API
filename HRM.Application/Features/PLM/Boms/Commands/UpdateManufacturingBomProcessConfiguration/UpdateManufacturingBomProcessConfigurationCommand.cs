using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.UpdateManufacturingBomProcessConfiguration;

public sealed record UpdateManufacturingBomProcessConfigurationCommand(
    Guid BomVersionId,
    UpdateManufacturingBomProcessConfigurationRequest Request)
    : IRequest<OperationResult<BomVersionDto>>;
