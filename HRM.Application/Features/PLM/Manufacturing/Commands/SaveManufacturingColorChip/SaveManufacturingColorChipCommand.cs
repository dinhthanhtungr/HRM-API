using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords.GetDtos;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.SaveManufacturingColorChip;

public sealed record SaveManufacturingColorChipCommand(Guid? Id, ManufacturingColorChipInput Request) : IRequest<OperationResult<GetColorChipManufacturingRecord>>;
