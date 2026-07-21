using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CreateCustomerInteraction;

/// <summary>
/// Tạo lịch sử tương tác khách hàng trong phạm vi CRM mà người dùng được phép xem.
/// Nếu request có ngày hẹn chăm sóc tiếp theo, handler đồng thời tạo WorkTask follow-up
/// và cập nhật các ngày tổng hợp trên Customer.
/// </summary>
public sealed class CreateCustomerInteractionCommand : IRequest<OperationResult<Guid>>
{
    public CreateCustomerInteractionRequest Request { get; init; } = new();
}
