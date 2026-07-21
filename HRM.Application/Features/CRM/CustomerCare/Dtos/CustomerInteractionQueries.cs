using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

/// <summary>
/// Bộ lọc và phân trang lịch sử tương tác khách hàng.
/// </summary>
public sealed class CustomerInteractionQuery : PaginationQuery
{
    public Guid? AssignedSaleEmployeeId { get; init; }
    public CustomerInteractionType? InteractionType { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public bool IncludeInactive { get; init; }
}
