using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Domain.Entities.CustomerSchema;

public class CustomerInteractionAiSummary
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? SaleEmployeeId { get; set; }
    public Guid CompanyId { get; set; }
    public CustomerInteractionSummaryScope SummaryScope { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public int InteractionCount { get; set; }
    public string PreviousSummary { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string CustomerNeed { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty;
    public string SourceModel { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = "v1";
    public bool IsAiSuccess { get; set; }
    public bool IsAiSkipped { get; set; }
    public string? AiErrorMessage { get; set; }
    public DateTime? AiGeneratedDate { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public virtual Customer Customer { get; set; } = null!;
    public virtual Employee? SaleEmployee { get; set; }
    public virtual Company Company { get; set; } = null!;
    public virtual Employee CreatedByNavigation { get; set; } = null!;
    public virtual Employee? UpdatedByNavigation { get; set; }
}
