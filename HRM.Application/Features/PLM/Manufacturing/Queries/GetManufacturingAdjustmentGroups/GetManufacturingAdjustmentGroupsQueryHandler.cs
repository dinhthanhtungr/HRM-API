using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingAdjustmentGroups;

internal sealed class GetManufacturingAdjustmentGroupsQueryHandler(ManufacturingAdjustmentService service) : IRequestHandler<GetManufacturingAdjustmentGroupsQuery, OperationResult<GetManufacturingFormulaAdjustmentBatchGroups>>
{
    public Task<OperationResult<GetManufacturingFormulaAdjustmentBatchGroups>> Handle(GetManufacturingAdjustmentGroupsQuery request, CancellationToken ct) => service.GroupsAsync(request.Id, request.ByOrder, ct);
}
