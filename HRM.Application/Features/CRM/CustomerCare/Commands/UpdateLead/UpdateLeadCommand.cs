using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.UpdateLead;

/// <summary>
/// Feature CRM CustomerCare - cập nhật khách hàng tiềm năng qua API PATCH
/// `/api/v1/crm/leads/{customerId}`. Command chỉ sửa customer còn là lead,
/// giữ nguyên trạng thái lead và không chuyển thành customer đã sale.
/// </summary>
public sealed record UpdateLeadCommand(Guid CustomerId, UpdateLeadRequest Request)
    : IRequest<OperationResult>;
