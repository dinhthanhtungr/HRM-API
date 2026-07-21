namespace HRM.Domain.Entities.CustomerSchema;

public partial class Customer
{
    public ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
}
