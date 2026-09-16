using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.ExplodeBom;

internal sealed class ExplodeBomQueryHandler
    : IRequestHandler<ExplodeBomQuery, OperationResult<BomExplosionDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ExplodeBomQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<BomExplosionDto>> Handle(
        ExplodeBomQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<BomExplosionDto>.Fail("Current company is invalid.");
        }
        if (request.BomVersionId == Guid.Empty || request.OutputQuantity <= 0 || request.MaxDepth is < 1 or > 20)
        {
            return OperationResult<BomExplosionDto>.Fail(
                "BomVersionId, positive OutputQuantity, and MaxDepth from 1 to 20 are required.");
        }

        var root = await _dbContext.BomVersions.AsNoTracking()
            .Where(x => x.BomVersionId == request.BomVersionId &&
                        x.BomDefinition.CompanyId == companyId &&
                        x.BomDefinition.BomType == BomType.Engineering &&
                        x.BomDefinition.IsActive &&
                        x.Status == BomVersionStatus.Released)
            .Select(x => new VersionRow(
                x.BomVersionId,
                x.BomDefinition.ProductId,
                x.VersionNo,
                x.BaseOutputQuantity,
                x.OutputUnit,
                x.EffectiveFrom,
                x.EffectiveTo))
            .FirstOrDefaultAsync(cancellationToken);
        if (root is null)
        {
            return OperationResult<BomExplosionDto>.Fail("Released Engineering BOM version was not found.");
        }

        var versions = await _dbContext.BomVersions.AsNoTracking()
            .Where(x => x.BomDefinition.CompanyId == companyId &&
                        x.BomDefinition.BomType == BomType.Engineering &&
                        x.BomDefinition.IsActive &&
                        x.Status == BomVersionStatus.Released)
            .Select(x => new VersionRow(
                x.BomVersionId,
                x.BomDefinition.ProductId,
                x.VersionNo,
                x.BaseOutputQuantity,
                x.OutputUnit,
                x.EffectiveFrom,
                x.EffectiveTo))
            .ToListAsync(cancellationToken);
        if (versions.All(x => x.BomVersionId != root.BomVersionId))
        {
            versions.Add(root);
        }

        var versionIds = versions.Select(x => x.BomVersionId).Distinct().ToArray();
        var itemRows = await _dbContext.BomVersionItems.AsNoTracking()
            .Where(x => versionIds.Contains(x.BomVersionId))
            .OrderBy(x => x.LineNo)
            .Select(x => new ItemRow(
                x.BomVersionId,
                x.LineNo,
                x.ItemType,
                x.MaterialId ?? x.ComponentProductId ?? Guid.Empty,
                x.Quantity,
                x.Unit,
                x.MaterialExternalIdSnapshot,
                x.MaterialNameSnapshot))
            .ToListAsync(cancellationToken);

        var effectiveAt = DateTime.Now;
        var effectiveVersionByProduct = versions
            .Where(x => (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= effectiveAt) &&
                        (!x.EffectiveTo.HasValue || x.EffectiveTo > effectiveAt))
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(v => v.VersionNo).First());
        var itemsByVersion = itemRows.GroupBy(x => x.BomVersionId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<ItemRow>)x.OrderBy(item => item.LineNo).ToList());

        try
        {
            var items = Expand(
                root,
                request.OutputQuantity,
                1,
                request.MaxDepth,
                new HashSet<Guid> { root.ProductId },
                effectiveVersionByProduct,
                itemsByVersion);
            return OperationResult<BomExplosionDto>.Ok(new BomExplosionDto
            {
                BomVersionId = root.BomVersionId,
                ProductId = root.ProductId,
                RequestedOutputQuantity = request.OutputQuantity,
                OutputUnit = root.OutputUnit,
                Items = items
            });
        }
        catch (InvalidOperationException exception)
        {
            return OperationResult<BomExplosionDto>.Fail(exception.Message);
        }
    }

    private static IReadOnlyList<BomExplosionItemDto> Expand(
        VersionRow version,
        decimal outputQuantity,
        int level,
        int maxDepth,
        IReadOnlySet<Guid> productPath,
        IReadOnlyDictionary<Guid, VersionRow> effectiveVersionByProduct,
        IReadOnlyDictionary<Guid, IReadOnlyList<ItemRow>> itemsByVersion)
    {
        if (!itemsByVersion.TryGetValue(version.BomVersionId, out var rows)) return [];
        var scale = outputQuantity / version.BaseOutputQuantity;

        return rows.Select(row =>
        {
            var required = decimal.Round(row.Quantity * scale, 3, MidpointRounding.AwayFromZero);
            VersionRow? childVersion = null;
            IReadOnlyList<BomExplosionItemDto> children = [];
            if (row.ItemType == ItemType.Product && effectiveVersionByProduct.TryGetValue(row.ItemId, out childVersion))
            {
                if (productPath.Contains(row.ItemId))
                    throw new InvalidOperationException("Cyclic Product dependency detected while exploding BOM.");
                if (level >= maxDepth)
                    throw new InvalidOperationException("BOM explosion exceeded MaxDepth.");
                var childPath = productPath.ToHashSet();
                childPath.Add(row.ItemId);
                children = Expand(childVersion, required, level + 1, maxDepth, childPath, effectiveVersionByProduct, itemsByVersion);
            }

            return new BomExplosionItemDto
            {
                Level = level,
                LineNo = row.LineNo,
                ItemType = row.ItemType,
                ItemId = row.ItemId,
                ItemCode = row.Code,
                ItemName = row.Name,
                RequiredQuantity = required,
                Unit = row.Unit,
                ExpandedFromBomVersionId = childVersion?.BomVersionId,
                Children = children
            };
        }).ToList();
    }

    private sealed record VersionRow(
        Guid BomVersionId,
        Guid ProductId,
        int VersionNo,
        decimal BaseOutputQuantity,
        string OutputUnit,
        DateTime? EffectiveFrom,
        DateTime? EffectiveTo);

    private sealed record ItemRow(
        Guid BomVersionId,
        int LineNo,
        ItemType ItemType,
        Guid ItemId,
        decimal Quantity,
        string Unit,
        string? Code,
        string? Name);
}
