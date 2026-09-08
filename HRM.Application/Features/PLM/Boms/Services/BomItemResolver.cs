using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Models;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Services;

/// <summary>
/// Xác thực component theo company scope và lấy snapshot hiển thị cho item của BOM.
/// </summary>
internal sealed class BomItemResolver
{
    private readonly IPLMWriteDbContext _dbContext;

    public BomItemResolver(IPLMWriteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    internal async Task<BomItemResolution> ResolveAsync(
        Guid parentProductId,
        IReadOnlyList<BomItemWriteDto> items,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var materialIds = items
            .Where(x => x.ItemType == ItemType.Material)
            .Select(x => x.ItemId)
            .Distinct()
            .ToArray();

        var productIds = items
            .Where(x => x.ItemType == ItemType.Product)
            .Select(x => x.ItemId)
            .Distinct()
            .ToArray();

        if (productIds.Contains(parentProductId))
        {
            return BomItemResolution.Fail(
                "A BOM cannot contain its own Product as a component.");
        }

        var materials = await _dbContext.Materials
            .AsNoTracking()
            .Where(x =>
                materialIds.Contains(x.MaterialId) &&
                x.CompanyId == companyId &&
                x.IsActive == true)
            .Select(x => new SourceItem(
                x.MaterialId,
                x.CategoryId,
                x.ExternalId,
                x.Name))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(x =>
                productIds.Contains(x.ProductId) &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new SourceItem(
                x.ProductId,
                x.CategoryId,
                x.Code,
                x.Name))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (materials.Count != materialIds.Length || products.Count != productIds.Length)
        {
            return BomItemResolution.Fail(
                "One or more BOM items are inactive or outside your company.");
        }

        var resolvedItems = items
            .Select(item => ResolveItem(item, materials, products))
            .ToList();

        return BomItemResolution.Ok(resolvedItems);
    }

    private static BomResolvedItem ResolveItem(
        BomItemWriteDto item,
        IReadOnlyDictionary<Guid, SourceItem> materials,
        IReadOnlyDictionary<Guid, SourceItem> products)
    {
        var source = item.ItemType == ItemType.Material
            ? materials[item.ItemId]
            : products[item.ItemId];

        return new BomResolvedItem(
            item.ItemType,
            item.ItemId,
            source.CategoryId,
            item.Quantity,
            item.Unit.Trim(),
            source.Code,
            source.Name,
            BomRules.NormalizeOptionalText(item.Note));
    }

    private sealed record SourceItem(
        Guid Id,
        Guid CategoryId,
        string? Code,
        string? Name);
}
