using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.ListManufacturingAdjustments;

internal sealed class ListManufacturingAdjustmentsQueryHandler(ManufacturingAdjustmentService service) : IRequestHandler<ListManufacturingAdjustmentsQuery, OperationResult<List<GetManufacturingFormulaAdjustment>>>
{
    public Task<OperationResult<List<GetManufacturingFormulaAdjustment>>> Handle(ListManufacturingAdjustmentsQuery request, CancellationToken ct) => service.ListAsync(request, ct);
}
