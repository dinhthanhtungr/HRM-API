using HRM.Application.Commons.Pagination;

namespace HRM.Application.Features.PLM.Materials.Dtos;

public sealed class SupplierSummaryDto
{
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public DateTime? LatestOrderDate { get; init; }
}

public sealed class SupplierDetailDto
{
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public string? RegistrationNumber { get; init; }
    public string? RegistrationAddress { get; init; }
    public string? TaxNumber { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
    public string? Note { get; init; }
    public DateTime? IssueDate { get; init; }
    public string? IssuedPlace { get; init; }
    public string? FaxNumber { get; init; }
    public DateTime? LatestOrderDate { get; init; }
    public IReadOnlyList<SupplierContactDto> Contacts { get; init; } = [];
    public IReadOnlyList<SupplierAddressDto> Addresses { get; init; } = [];
    public PagedResult<SupplierMaterialDto> Materials { get; set; } = default!;
}

public sealed class SupplierContactDto
{
    public Guid ContactId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class SupplierAddressDto
{
    public Guid AddressId { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Province { get; init; }
    public string? Country { get; init; }
    public bool IsPrimary { get; init; }
    public string? PostalCode { get; init; }
}

public sealed class SupplierMaterialDto
{
    public Guid MaterialId { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public decimal? CurrentPrice { get; init; }
    public string? Currency { get; init; }
    public bool IsPreferred { get; init; }
    public int? MinDeliveryDays { get; init; }
}
