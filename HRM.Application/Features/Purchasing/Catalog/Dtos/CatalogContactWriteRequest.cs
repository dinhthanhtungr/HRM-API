using System.ComponentModel.DataAnnotations;
using HRM.Domain.Enums;

namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogContactWriteRequest : CatalogPatchRequest
{
    [StringLength(250)]
    public string? FirstName { get; init; }
    [StringLength(250)]
    public string? LastName { get; init; }
    [StringLength(250)]
    public string? Gender { get; init; }
    [StringLength(250)]
    public string? Phone { get; init; }
    [StringLength(250)]
    [EmailAddress]
    public string? Email { get; init; }
    public bool? IsPrimary { get; init; }
}
