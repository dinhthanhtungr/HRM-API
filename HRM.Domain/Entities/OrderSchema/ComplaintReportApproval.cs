using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Orders;

namespace HRM.Domain.Entities.OrderSchema;

public sealed class ComplaintReportApproval
{
    public Guid ComplaintReportApprovalId { get; set; }
    public Guid ComplaintReportId { get; set; }
    public ComplaintApprovalStage Stage { get; set; }
    public ComplaintApprovalDecision Decision { get; set; }
    public Guid ActorId { get; set; }
    public string ActorNameSnapshot { get; set; } = string.Empty;
    public DateTime DecidedAt { get; set; }
    public string? Comment { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }

    public ComplaintReport ComplaintReport { get; set; } = null!;
    public Employee Actor { get; set; } = null!;
}
