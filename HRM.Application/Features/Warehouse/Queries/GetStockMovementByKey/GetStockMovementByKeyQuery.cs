using HRM.Application.Commons.Models;
using HRM.Application.Features.Warehouse.Dtos;
using MediatR;

namespace HRM.Application.Features.Warehouse.Queries.GetStockMovementByKey;

public sealed record GetStockMovementByKeyQuery(string MovementKey) : IRequest<OperationResult<StockMovementDetailDto>>;
