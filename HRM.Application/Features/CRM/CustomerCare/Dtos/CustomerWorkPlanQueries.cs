using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.WorkTaskEnums;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

/// <summary>
/// Bộ lọc và phân trang work plan theo khách hàng.
/// </summary>
public sealed class CustomerWorkPlanQuery : PaginationQuery
{
    public Guid? AssignedSaleEmployeeId { get; init; }
    public WorkPlanStatus? Status { get; init; }
    public WorkTaskPriority? Priority { get; init; }
    public bool IncludeInactive { get; init; }
}
