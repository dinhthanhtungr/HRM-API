using HRM.Application.Commons.Models;
using HRM.Application.Features.Warehouse.Dtos;
using MediatR;

namespace HRM.Application.Features.Warehouse.Queries.GetWarehouseVoucherById;

public sealed record GetWarehouseVoucherByIdQuery(long VoucherId, string? Keyword = null)
    : IRequest<OperationResult<WarehouseVoucherDetailResponseDto>>;
