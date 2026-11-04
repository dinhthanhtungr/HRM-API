using HRM.Domain.Enums;
namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogSupplierDetailDto
{
    public Guid SupplierId { get; init; }
    public int MaterialCount { get; init; }
    public bool IsActive { get; init; }
    public string? ExternalId { get; init; }
    public DateTime? CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
    public string? SupplierName { get; init; }
    public string? RegistrationNumber { get; init; }
    public string? RegistrationAddress { get; init; }
    public string? TaxNumber { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
    public string? Note { get; init; }
    public string? IssuedPlace { get; init; }
    public string? FaxNumber { get; init; }
    public SupplierPriority Priority { get; init; }
    public DateTime? IssueDate { get; init; }
    public IReadOnlyList<CatalogContactDto> Contacts { get; init; } = [];
    public IReadOnlyList<CatalogAddressDto> Addresses { get; init; } = [];
}
