using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrder;

/// <summary>
/// Tạo SaleOrder trong công ty hiện tại từ thông tin header và các dòng hàng do PLM cung cấp.
/// Backend tự sinh mã đơn, audit, tổng tiền và luôn khởi tạo đơn ở trạng thái New.
/// </summary>
public sealed record CreateSaleOrderCommand(
    CreateSaleOrderRequest Request) : IRequest<OperationResult<CreateSaleOrderResultDto>>;
