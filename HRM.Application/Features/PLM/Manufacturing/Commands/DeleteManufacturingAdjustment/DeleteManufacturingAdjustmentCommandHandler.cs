using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.DeleteManufacturingAdjustment;

internal sealed class DeleteManufacturingAdjustmentCommandHandler(ManufacturingAdjustmentService service) : IRequestHandler<DeleteManufacturingAdjustmentCommand, OperationResult>
{
    public Task<OperationResult> Handle(DeleteManufacturingAdjustmentCommand request, CancellationToken ct) => service.DeleteAsync(request.Id, request.BatchId, ct);
}
