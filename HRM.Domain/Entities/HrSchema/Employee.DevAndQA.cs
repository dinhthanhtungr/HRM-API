using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<EvaluationStandard> EvaluationStandardCreatedByNavigations { get; set; } = new List<EvaluationStandard>();
    public virtual ICollection<EvaluationStandard> EvaluationStandardUpdatedByNavigations { get; set; } = new List<EvaluationStandard>();
}
