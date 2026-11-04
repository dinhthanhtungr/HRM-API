using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.SaveManufacturingAdjustment;

public sealed record SaveManufacturingAdjustmentCommand(
    Guid OrderId,
    Guid? Id,
    ManufacturingAdjustmentInput Request
) : IRequest<OperationResult<GetManufacturingFormulaAdjustment>>;
