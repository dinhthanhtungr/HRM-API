using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Domain.Entities.CompanySchema;

public partial class Company
{
    public virtual ICollection<EvaluationStandard> EvaluationStandards { get; set; } = new List<EvaluationStandard>();
}
