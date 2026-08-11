using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

public sealed class TransferCustomersRequest
{
    public Guid? FromEmployeeId { get; init; }
    public Guid? FromGroupId { get; init; }
    public Guid ToEmployeeId { get; init; }
    public Guid? ToGroupId { get; init; }
    public bool TransferAll { get; init; }
    public IReadOnlyList<Guid> CustomerIds { get; init; } = [];
    public TransferType TransferType { get; init; }
    public string? Note { get; init; }
}

public sealed class TransferCustomersResultDto
{
    public Guid TransferLogId { get; init; }
    public int TransferredCount { get; init; }
}

public sealed class ResolveTransferSourceRequest
{
    public IReadOnlyList<Guid> CustomerIds { get; init; } = [];
    public TransferType TransferType { get; init; }
}

public sealed class TransferSourceResolutionDto
{
    public Guid FromEmployeeId { get; init; }
    public string FromEmployeeName { get; init; } = string.Empty;
    public Guid FromGroupId { get; init; }
    public string? FromGroupName { get; init; }
    public int CustomerCount { get; init; }
}

public sealed class CustomerTransferWorkspaceDto
{
    public IReadOnlyList<CustomerTransferEmployeeOptionDto> SourceEmployees { get; init; } = [];
    public IReadOnlyList<CustomerTransferEmployeeOptionDto> TargetEmployees { get; init; } = [];
    public PagedResult<CustomerTransferCustomerOptionDto> Customers { get; init; } = default!;
    public CustomerTransferOwnerDto? ResolvedSource { get; init; }
    public CustomerTransferWorkspaceSummaryDto Summary { get; init; } = new();
}

public sealed class CustomerTransferCustomerLookupDto
{
    public PagedResult<CustomerTransferCustomerOptionDto> Customers { get; init; } = default!;
    public CustomerTransferOwnerDto? ResolvedSource { get; init; }
    public CustomerTransferWorkspaceSummaryDto Summary { get; init; } = new();
}

public sealed class CustomerTransferEmployeeOptionDto
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public Guid? GroupId { get; init; }
    public string? GroupName { get; init; }
    public int CustomerCount { get; init; }
    public int LeadCount { get; init; }
    public int SaledCount { get; init; }
}

public sealed class CustomerTransferCustomerOptionDto
{
    public Guid CustomerId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public bool IsLead { get; init; }
    public TransferType TransferType { get; init; }
    public Guid SourceEmployeeId { get; init; }
    public string SourceEmployeeName { get; init; } = string.Empty;
    public Guid SourceGroupId { get; init; }
    public string? SourceGroupName { get; init; }
    public DateTime? LeadExpiresAt { get; init; }
}

public sealed class CustomerTransferOwnerDto
{
    public Guid SourceEmployeeId { get; init; }
    public string SourceEmployeeName { get; init; } = string.Empty;
    public Guid SourceGroupId { get; init; }
    public string? SourceGroupName { get; init; }
    public TransferType TransferType { get; init; }
    public bool IsLead { get; init; }
}

public sealed class CustomerTransferWorkspaceSummaryDto
{
    public int TotalCount { get; init; }
    public int LeadCount { get; init; }
    public int SaledCount { get; init; }
}

public sealed class ExecuteCustomerTransferRequest
{
    public Guid? SourceEmployeeId { get; init; }
    public Guid? SourceCustomerId { get; init; }
    public Guid ToEmployeeId { get; init; }
    public Guid? ToGroupId { get; init; }
    public bool TransferAll { get; init; }
    public IReadOnlyList<Guid> CustomerIds { get; init; } = [];
    public string? Note { get; init; }
}

public sealed class ExecuteCustomerTransferResultDto
{
    public int TotalCount { get; init; }
    public int LeadCount { get; init; }
    public int SaledCount { get; init; }
    public int TransferredCount { get; init; }
    public IReadOnlyList<TransferCustomersResultDto> Logs { get; init; } = [];
}
