using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Orders;

namespace HRM.Domain.Entities.OrderSchema;

public sealed class ComplaintCapaAction
{
    public Guid ComplaintCapaActionId { get; set; }
    public Guid ComplaintReportId { get; set; }
    public ComplaintCapaActionType ActionType { get; set; }
    public int SortOrder { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? PersonInChargeId { get; set; }
    public string? PersonInChargeNameSnapshot { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Result { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedDate { get; set; }
    public Guid UpdatedBy { get; set; }

    public ComplaintReport ComplaintReport { get; set; } = null!;
    public Employee? PersonInCharge { get; set; }
}
