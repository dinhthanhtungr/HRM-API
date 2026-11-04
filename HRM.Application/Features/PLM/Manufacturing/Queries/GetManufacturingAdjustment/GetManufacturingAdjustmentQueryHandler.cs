using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingAdjustment;

internal sealed class GetManufacturingAdjustmentQueryHandler(ManufacturingAdjustmentService service) : IRequestHandler<GetManufacturingAdjustmentQuery, OperationResult<GetManufacturingFormulaAdjustment>>
{
    public Task<OperationResult<GetManufacturingFormulaAdjustment>> Handle(GetManufacturingAdjustmentQuery request, CancellationToken ct) => service.GetAsync(request.Id, ct);
}
