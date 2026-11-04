using System.ComponentModel.DataAnnotations;
using HRM.Domain.Enums;

namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogMaterialWriteRequest : CatalogPatchRequest
{
    [Required, MaxLength(100)]
    public IReadOnlyList<CatalogSupplierPriceWriteRequest>? SupplierPrices { get; init; } = [];
    [StringLength(100)]
    public string? ExternalId { get; init; }
    [StringLength(500)]
    public string? Name { get; init; }
    [StringLength(250)]
    public string? CustomCode { get; init; }
    [StringLength(250)]
    public string? Unit { get; init; }
    [StringLength(250)]
    public string? Package { get; init; }
    [StringLength(2000)]
    public string? Comment { get; init; }
    [StringLength(250)]
    public string? Barcode { get; init; }
    public Guid? CategoryId { get; init; }
    [Range(0, double.MaxValue)]
    public double? Weight { get; init; }
    [Range(0, double.MaxValue)]
    public double? MinQuantity { get; init; }
}
