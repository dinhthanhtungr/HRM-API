using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<Formula> FormulaCreatedByNavigations { get; set; } = new List<Formula>();
    public virtual ICollection<Formula> FormulaUpdatedByNavigations { get; set; } = new List<Formula>();
    public virtual ICollection<Formula> FormulaCheckByNavigations { get; set; } = new List<Formula>();
    public virtual ICollection<Formula> FormulaSentByNavigations { get; set; } = new List<Formula>();
    public virtual ICollection<SampleRequest> SampleRequestCreatedByNavigations { get; set; } = new List<SampleRequest>();
    public virtual ICollection<SampleRequest> SampleRequestManagerByNavigations { get; set; } = new List<SampleRequest>();
    public virtual ICollection<SampleRequest> SampleRequestUpdatedByNavigations { get; set; } = new List<SampleRequest>();
    public virtual ICollection<SampleRequest> SampleRequestSendByNavigations { get; set; } = new List<SampleRequest>();
    public virtual ICollection<SampleRequest> SampleRequestLabReceivedByNavigations { get; set; } = new List<SampleRequest>();
    public virtual ICollection<Product> ProductCreatedByNavigations { get; set; } = new List<Product>();
    public virtual ICollection<Product> ProductUpdatedByNavigations { get; set; } = new List<Product>();
}
