using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<CustomerClaim> CustomerClaims { get; set; } = new List<CustomerClaim>();
    public virtual ICollection<CustomerNote> CustomerNotesAuthored { get; set; } = new List<CustomerNote>();
    public virtual ICollection<CustomerInteractionAiSummary> CustomerInteractionAiSummarySaleEmployees { get; set; } = new List<CustomerInteractionAiSummary>();
    public virtual ICollection<CustomerInteractionAiSummary> CustomerInteractionAiSummaryCreatedByNavigations { get; set; } = new List<CustomerInteractionAiSummary>();
    public virtual ICollection<CustomerInteractionAiSummary> CustomerInteractionAiSummaryUpdatedByNavigations { get; set; } = new List<CustomerInteractionAiSummary>();
    public virtual ICollection<CustomerAssignment> CustomerAssignmentCreatedByNavigations { get; set; } = new List<CustomerAssignment>();
    public virtual ICollection<CustomerAssignment> CustomerAssignmentEmployees { get; set; } = new List<CustomerAssignment>();
    public virtual ICollection<CustomerAssignment> CustomerAssignmentUpdatedByNavigations { get; set; } = new List<CustomerAssignment>();
    public virtual ICollection<Customer> CustomerCreatedByNavigations { get; set; } = new List<Customer>();
    public virtual ICollection<CustomerTransferLog> CustomerTransferLogCreatedByNavigations { get; set; } = new List<CustomerTransferLog>();
    public virtual ICollection<CustomerTransferLog> CustomerTransferLogFromEmployees { get; set; } = new List<CustomerTransferLog>();
    public virtual ICollection<CustomerTransferLog> CustomerTransferLogToEmployees { get; set; } = new List<CustomerTransferLog>();
    public virtual ICollection<Customer> CustomerUpdatedByNavigations { get; set; } = new List<Customer>();
}
