namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

public sealed class ChangeDeliveryOrderStatusRequest
{
    public string? Status { get; init; }
}

public sealed class DeliveryOrderLifecycleResultDto
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime? UpdatedDate { get; init; }
}
