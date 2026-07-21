using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public ICollection<Quotation> QuotationSaleEmployeeNavigations { get; set; } = new List<Quotation>();
    public ICollection<Quotation> QuotationCreatedByNavigations { get; set; } = new List<Quotation>();
    public ICollection<Quotation> QuotationUpdatedByNavigations { get; set; } = new List<Quotation>();
    public ICollection<QuotationStatusHistory> QuotationStatusHistoryChangedByNavigations { get; set; } = new List<QuotationStatusHistory>();
}
