using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

public sealed class CustomerTransferLogDto
{
    public Guid TransferLogId { get; init; }
    public TransferType TransferType { get; init; }
    public DateTime CreatedDate { get; init; }
    public Guid CreatedBy { get; init; }
    public string CreatedByName { get; init; } = string.Empty;
    public Guid FromEmployeeId { get; init; }
    public string FromEmployeeName { get; init; } = string.Empty;
    public Guid ToEmployeeId { get; init; }
    public string ToEmployeeName { get; init; } = string.Empty;
    public Guid FromGroupId { get; init; }
    public string? FromGroupName { get; init; }
    public Guid ToGroupId { get; init; }
    public string? ToGroupName { get; init; }
    public string? Note { get; init; }
    public int CustomerCount { get; init; }
    public IReadOnlyList<CustomerTransferLogCustomerDto> Customers { get; init; } = [];
}

public sealed class CustomerTransferLogCustomerDto
{
    public Guid CustomerId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public bool IsLead { get; init; }
    public string LeadStatus { get; init; } = string.Empty;
}
