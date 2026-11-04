using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords.GetDtos;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords;
using HRM.Application.Features.PLM.Manufacturing.Services.ColorChips;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.SaveManufacturingColorChip;

internal sealed class SaveManufacturingColorChipCommandHandler(ManufacturingColorChipService service) : IRequestHandler<SaveManufacturingColorChipCommand, OperationResult<GetColorChipManufacturingRecord>>
{
    public Task<OperationResult<GetColorChipManufacturingRecord>> Handle(SaveManufacturingColorChipCommand request, CancellationToken ct) => service.SaveAsync(request.Id, request.Request, ct);
}
