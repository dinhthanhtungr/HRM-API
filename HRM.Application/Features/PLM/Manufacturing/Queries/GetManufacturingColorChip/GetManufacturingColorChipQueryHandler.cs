using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords.GetDtos;
using HRM.Application.Features.PLM.Manufacturing.Services.ColorChips;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingColorChip;

internal sealed class GetManufacturingColorChipQueryHandler(ManufacturingColorChipService service) : IRequestHandler<GetManufacturingColorChipQuery, OperationResult<GetColorChipManufacturingRecord>>
{
    public Task<OperationResult<GetColorChipManufacturingRecord>> Handle(GetManufacturingColorChipQuery request, CancellationToken ct) => service.GetAsync(request.Id, ct);
}
