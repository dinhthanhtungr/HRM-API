using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.UpdateManufacturingBomMachineParameters;

public sealed record UpdateManufacturingBomMachineParametersCommand(
    Guid BomVersionId,
    UpdateManufacturingBomMachineParametersRequest Request)
    : IRequest<OperationResult<IReadOnlyList<ManufacturingBomStageMachineParameterDto>>>;
