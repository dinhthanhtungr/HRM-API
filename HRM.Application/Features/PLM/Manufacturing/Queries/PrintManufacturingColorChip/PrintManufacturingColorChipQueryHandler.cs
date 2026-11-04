using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords.GetDtos;
using HRM.Application.Features.PLM.Manufacturing.Services.ColorChips;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.PrintManufacturingColorChip;

internal sealed class PrintManufacturingColorChipQueryHandler(ManufacturingColorChipService service) : IRequestHandler<PrintManufacturingColorChipQuery, OperationResult<byte[]>>
{
    public Task<OperationResult<byte[]>> Handle(PrintManufacturingColorChipQuery request, CancellationToken ct) => service.PrintAsync(request.Id, ct);
}
