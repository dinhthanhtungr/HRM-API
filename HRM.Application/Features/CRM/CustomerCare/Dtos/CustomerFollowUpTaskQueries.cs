using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.WorkTaskEnums;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

/// <summary>
/// Bộ lọc và phân trang follow-up task CRM.
/// </summary>
public sealed class CustomerFollowUpTaskQuery : PaginationQuery
{
    public Guid? AssignedSaleEmployeeId { get; init; }
    public WorkTaskStatus? Status { get; init; }
    public WorkTaskPriority? Priority { get; init; }
    public bool OnlyOverdue { get; init; }
    public DateTime? DueFrom { get; init; }
    public DateTime? DueTo { get; init; }
    public bool IncludeInactive { get; init; }
}
