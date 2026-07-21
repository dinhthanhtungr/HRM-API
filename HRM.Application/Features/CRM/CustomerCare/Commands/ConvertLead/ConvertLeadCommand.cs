using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.ConvertLead;

/// <summary>
/// Feature CRM CustomerCare - chuyển khách hàng tiềm năng thành khách hàng đã sale
/// qua API POST `/api/v1/crm/leads/{customerId}/convert`. Command hủy claim Work
/// còn active, tạo `CustomerAssignment` và đồng bộ `CurrentSaleId`.
/// </summary>
public sealed record ConvertLeadCommand(Guid CustomerId, ConvertLeadRequest Request)
    : IRequest<OperationResult>;
