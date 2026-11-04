using HRM.Domain.Enums;
namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogContactDto
{
    public Guid ContactId { get; init; }
    public bool IsActive { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Gender { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsPrimary { get; init; }
}
