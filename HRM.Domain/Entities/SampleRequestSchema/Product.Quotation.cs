using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Domain.Entities.SampleRequestSchema;

public partial class Product
{
    public ICollection<QuotationLine> QuotationLines { get; set; } = new List<QuotationLine>();
}
