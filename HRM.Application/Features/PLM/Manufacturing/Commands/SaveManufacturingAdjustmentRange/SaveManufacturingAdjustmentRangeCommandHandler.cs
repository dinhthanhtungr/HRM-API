using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.SaveManufacturingAdjustmentRange;

internal sealed class SaveManufacturingAdjustmentRangeCommandHandler(ManufacturingAdjustmentService service) : IRequestHandler<SaveManufacturingAdjustmentRangeCommand, OperationResult<GetManufacturingFormulaAdjustment>>
{
    public Task<OperationResult<GetManufacturingFormulaAdjustment>> Handle(SaveManufacturingAdjustmentRangeCommand request, CancellationToken ct) => service.SaveRangeAsync(request.Id, request.BatchId, request.Request, request.SingleBatchUpsert, ct);
}
