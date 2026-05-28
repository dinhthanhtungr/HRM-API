namespace HRM.Application.Features.Dispatch.Deliverers.Dtos;

public sealed class DelivererDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? DelivererType { get; init; }
    public string? Phone { get; init; }
    public string? Note { get; init; }
    public bool IsActive { get; init; }
}
