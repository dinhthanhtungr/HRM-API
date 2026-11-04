using System.ComponentModel.DataAnnotations;
using HRM.Domain.Enums;

namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogSupplierWriteRequest : CatalogPatchRequest
{
    [StringLength(100)]
    public string? ExternalId { get; init; }
    [StringLength(500)]
    public string? SupplierName { get; init; }
    [StringLength(250)]
    public string? RegistrationNumber { get; init; }
    [StringLength(2000)]
    public string? RegistrationAddress { get; init; }
    [StringLength(250)]
    public string? TaxNumber { get; init; }
    [StringLength(250)]
    public string? Phone { get; init; }
    [StringLength(250)]
    public string? Website { get; init; }
    [StringLength(2000)]
    public string? Note { get; init; }
    [StringLength(250)]
    public string? IssuedPlace { get; init; }
    [StringLength(250)]
    public string? FaxNumber { get; init; }
    [EnumDataType(typeof(SupplierPriority))]
    public SupplierPriority? Priority { get; init; }
    public DateTime? IssueDate { get; init; }
}
