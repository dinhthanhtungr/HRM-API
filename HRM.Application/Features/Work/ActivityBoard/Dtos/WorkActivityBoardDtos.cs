using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.Work.ActivityBoard.Dtos;

/// <summary>
/// Điều kiện đọc danh sách khách hàng có hoạt động để hiển thị panel trái của activity board.
/// Query chỉ trả summary, không hydrate toàn bộ detail task/plan/interaction.
/// </summary>
public sealed class WorkActivityBoardCustomerListQueryDto : PaginationQuery
{
    public bool OnlyHasActivity { get; init; } = true;
    public bool OnlyMine { get; init; }
    public bool IncludeCompleted { get; init; } = true;
    public bool IncludeInactive { get; init; } 
    public IReadOnlyCollection<CustomerCrmCalendarSourceType>? ActivityTypes { get; init; }
}

public sealed class WorkActivityBoardCustomerSummaryDto
{
    public Guid CustomerId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public bool IsLead { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public int OpenTaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public int WorkPlanCount { get; set; }
    public int InteractionCount { get; set; }
    public int OverdueCount { get; set; }
}
