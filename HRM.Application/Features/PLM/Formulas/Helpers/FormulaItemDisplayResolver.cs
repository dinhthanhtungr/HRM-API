using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Helpers;

/// <summary>
/// Tải và định dạng thông tin item hiện hành cho các API đọc Formula theo batch.
/// </summary>
internal static class FormulaItemDisplayResolver
{
    public static async Task<IReadOnlyDictionary<FormulaItemDisplayKey, FormulaItemCurrentData>>
        LoadCurrentDataAsync(
            IPLMReadDbContext dbContext,
            Guid companyId,
            IEnumerable<FormulaItemDisplaySource> sources,
            CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return new Dictionary<FormulaItemDisplayKey, FormulaItemCurrentData>();
        }

        var sourceList = sources
            .Where(source => source.ItemId != Guid.Empty)
            .ToList();

        var result = new Dictionary<FormulaItemDisplayKey, FormulaItemCurrentData>();
        var materialIds = sourceList
            .Where(source => IsMaterial(source.ItemType))
            .Select(source => source.ItemId)
            .Distinct()
            .ToList();
        var productIds = sourceList
            .Where(source => !IsMaterial(source.ItemType))
            .Select(source => source.ItemId)
            .Distinct()
            .ToList();

        if (materialIds.Count > 0)
        {
            var materials = await dbContext.Materials
                .AsNoTracking()
                .Where(material =>
                    material.CompanyId == companyId &&
                    materialIds.Contains(material.MaterialId))
                .Select(material => new
                {
                    material.MaterialId,
                    material.Name,
                    material.ExternalId
                })
                .ToListAsync(cancellationToken);

            foreach (var material in materials)
            {
                result[new FormulaItemDisplayKey(true, material.MaterialId)] =
                    new FormulaItemCurrentData(material.Name, material.ExternalId, null);
            }
        }

        if (productIds.Count > 0)
        {
            var products = await dbContext.Products
                .AsNoTracking()
                .Where(product =>
                    product.CompanyId == companyId &&
                    productIds.Contains(product.ProductId))
                .Select(product => new
                {
                    product.ProductId,
                    product.Name,
                    product.ColourCode,
                    SampleRequestExternalId = product.SampleRequests
                        .Where(sampleRequest =>
                            sampleRequest.IsActive &&
                            sampleRequest.CompanyId == companyId)
                        .OrderByDescending(sampleRequest => sampleRequest.CreatedDate)
                        .Select(sampleRequest => sampleRequest.ExternalId)
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            foreach (var product in products)
            {
                result[new FormulaItemDisplayKey(false, product.ProductId)] =
                    new FormulaItemCurrentData(
                        product.Name,
                        product.SampleRequestExternalId,
                        product.ColourCode);
            }
        }

        return result;
    }

    public static FormulaItemDisplay Resolve(
        FormulaItemDisplaySource source,
        IReadOnlyDictionary<FormulaItemDisplayKey, FormulaItemCurrentData> currentData)
    {
        var isMaterial = IsMaterial(source.ItemType);
        if (!currentData.TryGetValue(new FormulaItemDisplayKey(isMaterial, source.ItemId), out var current))
        {
            return new FormulaItemDisplay(source.NameSnapshot, source.ExternalIdSnapshot);
        }

        if (isMaterial)
        {
            return new FormulaItemDisplay(
                FirstNotBlank(current.Name, source.NameSnapshot),
                FirstNotBlank(current.ExternalId, source.ExternalIdSnapshot));
        }

        return new FormulaItemDisplay(
            FormatProductName(current.ColourCode, current.Name, source.NameSnapshot),
            FirstNotBlank(current.ExternalId, source.ExternalIdSnapshot));
    }

    private static bool IsMaterial(ItemType itemType)
        => itemType is ItemType.Material or ItemType.MaterialFailure;

    private static string? FormatProductName(
        string? colourCode,
        string? name,
        string? fallbackName)
    {
        var normalizedName = FirstNotBlank(name, fallbackName);
        var normalizedColourCode = TrimToNull(colourCode);

        return normalizedColourCode is null
            ? normalizedName
            : normalizedName is null
                ? $"[{normalizedColourCode}]"
                : $"[{normalizedColourCode}] {normalizedName}";
    }

    private static string? FirstNotBlank(string? preferred, string? fallback)
        => TrimToNull(preferred) ?? TrimToNull(fallback);

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal readonly record struct FormulaItemDisplayKey(bool IsMaterial, Guid ItemId);

internal sealed record FormulaItemDisplaySource(
    Guid ItemId,
    ItemType ItemType,
    string? NameSnapshot,
    string? ExternalIdSnapshot);

internal sealed record FormulaItemCurrentData(
    string? Name,
    string? ExternalId,
    string? ColourCode);

internal sealed record FormulaItemDisplay(string? Name, string? ExternalId);
