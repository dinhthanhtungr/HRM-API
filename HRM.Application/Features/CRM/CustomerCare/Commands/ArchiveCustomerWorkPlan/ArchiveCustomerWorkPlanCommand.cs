using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.ArchiveCustomerWorkPlan;

/// <summary>
/// Archive work plan bằng soft delete.
/// </summary>
public sealed record ArchiveCustomerWorkPlanCommand(Guid PlanId) : IRequest<OperationResult>;
