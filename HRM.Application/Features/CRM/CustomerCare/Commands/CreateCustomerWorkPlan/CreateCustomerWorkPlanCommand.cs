using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CreateCustomerWorkPlan;

/// <summary>
/// Tạo kế hoạch chăm sóc khách hàng dài hạn trên WorkPlanSchema.
/// </summary>
public sealed class CreateCustomerWorkPlanCommand : IRequest<OperationResult<Guid>>
{
    public CreateCustomerWorkPlanRequest Request { get; init; } = new();
}
