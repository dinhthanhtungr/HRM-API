using HRM.Domain.Enums;
namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogAddressDto
{
    public Guid AddressId { get; init; }
    public bool IsActive { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Province { get; init; }
    public string? Country { get; init; }
    public string? PostalCode { get; init; }
    public bool IsPrimary { get; init; }
}
