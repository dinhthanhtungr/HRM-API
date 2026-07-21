namespace HRM.Application.Features.CRM.Customers.Dtos.GetCustomerLookup;

public sealed class CustomerLookupDto
{
    public Guid CustomerId { get; set; }
    public bool IsLead { get; init; }
    public string ExternalId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
}
