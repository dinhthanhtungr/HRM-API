namespace HRM.Domain.Entities.CustomerSchema;

public partial class Contact
{
    public ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
}
