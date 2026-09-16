using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ProductionOrders.Queries.CheckProductInProduction;

/// <summary>
/// Kiểm tra Product có lệnh sản xuất active đang ở trạng thái thực thi trong công ty hiện tại hay không.
/// </summary>
public sealed record CheckProductInProductionQuery(Guid ProductId) : IRequest<ProductInProductionDto>;
