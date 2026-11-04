using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using MediatR;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.CalculateManufacturingAdjustment;

internal sealed class CalculateManufacturingAdjustmentQueryHandler(ManufacturingAdjustmentService service)
    : IRequestHandler<CalculateManufacturingAdjustmentQuery, OperationResult<ManufacturingAdjustmentCalculation>>
{
    public Task<OperationResult<ManufacturingAdjustmentCalculation>> Handle(CalculateManufacturingAdjustmentQuery request, CancellationToken ct)
        => service.CalculateDraftAsync(request.OrderId, request.Request, ct, request.AdjustmentId);
}
