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
