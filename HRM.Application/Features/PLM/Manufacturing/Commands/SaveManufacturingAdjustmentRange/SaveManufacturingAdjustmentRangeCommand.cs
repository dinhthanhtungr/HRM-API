using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.SaveManufacturingAdjustmentRange;

public sealed record SaveManufacturingAdjustmentRangeCommand(
    Guid Id,
    Guid? BatchId,
    ManufacturingAdjustmentRangeInput Request,
    bool SingleBatchUpsert = false
) : IRequest<OperationResult<GetManufacturingFormulaAdjustment>>;
