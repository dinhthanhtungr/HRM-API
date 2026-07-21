using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.ClaimLead;

/// <summary>
/// Feature CRM CustomerCare - thêm hoặc gia hạn người chăm sóc khách hàng tiềm năng qua API
/// POST `/api/v1/crm/leads/{customerId}/claim`. Command tạo claim Work mới hoặc gia hạn claim active
/// của cùng employee/group sau khi kiểm tra visibility, company và quyền group.
/// </summary>
public sealed record ClaimLeadCommand(Guid CustomerId, ClaimLeadRequest Request)
    : IRequest<OperationResult>;
