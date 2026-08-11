using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetCustomerProductStock;

/// <summary>
/// Lấy tồn thành phẩm theo customer/product bằng đường truy vết VA lot -> ProductionSelect -> MFG.
/// </summary>
public sealed record GetCustomerProductStockQuery(
    Guid CustomerId,
    Guid ProductId) : IRequest<OperationResult<CustomerProductStockSummaryDto>>;
