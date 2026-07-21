using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Domain.Entities.CompanySchema;

public partial class Company
{
    public ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
}
