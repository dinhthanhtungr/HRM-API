using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using MediatR;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.InitializeManufacturingAdjustment;

internal sealed class InitializeManufacturingAdjustmentQueryHandler(ManufacturingAdjustmentService service)
    : IRequestHandler<InitializeManufacturingAdjustmentQuery, OperationResult<ManufacturingAdjustmentEditor>>
{
    public Task<OperationResult<ManufacturingAdjustmentEditor>> Handle(InitializeManufacturingAdjustmentQuery request, CancellationToken ct)
        => service.InitializeAsync(request.OrderId, request.AdjustmentId, ct);
}
