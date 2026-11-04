using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords.GetDtos;
using HRM.Application.Features.PLM.Manufacturing.Services.ColorChips;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.ListManufacturingColorChips;

internal sealed class ListManufacturingColorChipsQueryHandler(ManufacturingColorChipService service) : IRequestHandler<ListManufacturingColorChipsQuery, OperationResult<PagedResult<GetColorChipManufacturingRecordSummary>>>
{
    public Task<OperationResult<PagedResult<GetColorChipManufacturingRecordSummary>>> Handle(ListManufacturingColorChipsQuery request, CancellationToken ct) => service.ListAsync(request, ct);
}
