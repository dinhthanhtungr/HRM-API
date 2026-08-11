using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.ConvertLead;

/// <summary>
/// Command nội bộ chuyển khách hàng tiềm năng thành khách hàng đã sale.
/// Command không được public cho FE; feature backend phù hợp chịu trách nhiệm gọi và giữ audit nghiệp vụ.
/// </summary>
public sealed record ConvertLeadCommand(Guid CustomerId, ConvertLeadRequest Request)
    : IRequest<OperationResult>;
