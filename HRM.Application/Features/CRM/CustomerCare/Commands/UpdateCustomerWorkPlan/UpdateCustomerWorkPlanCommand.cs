using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.UpdateCustomerWorkPlan;

/// <summary>
/// Cập nhật các trường nghiệp vụ được phép của work plan.
/// </summary>
public sealed class UpdateCustomerWorkPlanCommand : IRequest<OperationResult>
{
    public Guid PlanId { get; init; }
    public UpdateCustomerWorkPlanRequest Request { get; init; } = new();
}
