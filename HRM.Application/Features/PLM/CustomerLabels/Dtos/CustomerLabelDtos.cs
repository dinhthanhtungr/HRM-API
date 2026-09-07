namespace HRM.Application.Features.PLM.CustomerLabels.Dtos;

public sealed class CustomerLabelDetailDto
{
    public Guid Id { get; init; }
    public int LineNo { get; init; }
    public string FieldKey { get; init; } = string.Empty;
    public string? FieldValue { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CustomerLabelDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string? ColorCode { get; init; }
    public Guid CustomerId { get; init; }
    public string? CustomerExternalId { get; init; }
    public string? LabelType { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public IReadOnlyList<CustomerLabelDetailDto> Details { get; init; } = Array.Empty<CustomerLabelDetailDto>();
}

public sealed class SaveCustomerLabelResultDto
{
    public Guid CustomerLabelHeaderId { get; init; }
    public DateTime? UpdatedDate { get; init; }
}

public sealed class SaveCustomerLabelDetailResultDto
{
    public Guid CustomerLabelDetailId { get; init; }
    public Guid CustomerLabelHeaderId { get; init; }
}
