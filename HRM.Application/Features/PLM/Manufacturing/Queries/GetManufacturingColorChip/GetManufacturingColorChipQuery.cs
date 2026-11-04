using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords.GetDtos;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingColorChip;

public sealed record GetManufacturingColorChipQuery(Guid Id) : IRequest<OperationResult<GetColorChipManufacturingRecord>>;
