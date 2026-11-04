using System.ComponentModel.DataAnnotations;
using HRM.Domain.Enums;

namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogAddressWriteRequest : CatalogPatchRequest
{
    [StringLength(2000)]
    public string? AddressLine { get; init; }
    [StringLength(250)]
    public string? City { get; init; }
    [StringLength(250)]
    public string? District { get; init; }
    [StringLength(250)]
    public string? Province { get; init; }
    [StringLength(250)]
    public string? Country { get; init; }
    [StringLength(250)]
    public string? PostalCode { get; init; }
    public bool? IsPrimary { get; init; }
}
