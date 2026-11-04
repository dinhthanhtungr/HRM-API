using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.SaveManufacturingAdjustment;

internal sealed class SaveManufacturingAdjustmentCommandHandler(ManufacturingAdjustmentService service) : IRequestHandler<SaveManufacturingAdjustmentCommand, OperationResult<GetManufacturingFormulaAdjustment>>
{
    public Task<OperationResult<GetManufacturingFormulaAdjustment>> Handle(SaveManufacturingAdjustmentCommand request, CancellationToken ct) => service.SaveAsync(request.OrderId, request.Id, request.Request, ct);
}
