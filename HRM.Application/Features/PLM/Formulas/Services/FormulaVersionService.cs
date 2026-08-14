using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Concurrency;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Services;

/// <summary>
/// Tạo snapshot Formula và lưu cùng transaction EF với các thay đổi nghiệp vụ đang được track.
/// </summary>
internal sealed class FormulaVersionService
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly KeyedMutationLock<Guid> _mutationLock;

    public FormulaVersionService(
        IPLMWriteDbContext dbContext,
        KeyedMutationLock<Guid> mutationLock)
    {
        _dbContext = dbContext;
        _mutationLock = mutationLock;
    }

    public async Task<FormulaVersion?> SaveSnapshotAsync(
        Formula formula,
        Guid employeeId,
        DateTime now,
        string? changeReason,
        bool force,
        CancellationToken cancellationToken)
    {
        using var mutationLease = await _mutationLock.AcquireAsync(
            formula.FormulaId,
            cancellationToken);

        var persistedMaterials = await _dbContext.FormulaMaterials
            .Where(x => x.FormulaId == formula.FormulaId && x.IsActive)
            .ToListAsync(cancellationToken);

        var activeMaterials = persistedMaterials
            .Concat(formula.FormulaMaterials)
            .GroupBy(x => x.FormulaMaterialId)
            .Select(x => x.First())
            .Where(x => x.IsActive)
            .OrderBy(x => x.LineNo)
            .ToList();

        EnsureUniqueLineNumbers(activeMaterials);

        var latestVersion = await _dbContext.FormulaVersions
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.FormulaId == formula.FormulaId)
            .OrderByDescending(x => x.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

        if (!force && latestVersion is not null &&
            SnapshotMatches(latestVersion, formula, activeMaterials))
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        var openVersions = await _dbContext.FormulaVersions
            .Where(x => x.FormulaId == formula.FormulaId && x.EffectiveTo == null)
            .ToListAsync(cancellationToken);

        CloseOpenVersions(openVersions, now);

        var version = CreateSnapshot(
            formula,
            activeMaterials,
            GetNextVersionNo(latestVersion),
            now,
            employeeId,
            NormalizeChangeReason(changeReason));

        await _dbContext.FormulaVersions.AddAsync(version, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return version;
    }

    internal static FormulaVersion CreateSnapshot(
        Formula formula,
        IReadOnlyCollection<FormulaMaterial> activeMaterials,
        int versionNo,
        DateTime now,
        Guid employeeId,
        string? changeReason)
    {
        var version = new FormulaVersion
        {
            FormulaVersionId = Guid.CreateVersion7(),
            FormulaId = formula.FormulaId,
            VersionNo = versionNo,
            Name = formula.Name,
            Status = formula.Status,
            Note = formula.Note,
            TotalPrice = RoundFormulaPrice(formula.TotalPrice),
            ProductionPrice = RoundFormulaPrice(formula.ProductionPrice),
            PresidentPrice = RoundFormulaPrice(formula.PresidentPrice),
            ProfitMarginPrice = RoundFormulaPrice(formula.ProfitMarginPrice),
            EffectiveFrom = now,
            CreatedAt = now,
            CreatedBy = employeeId,
            ChangeReason = changeReason
        };

        foreach (var material in activeMaterials.OrderBy(x => x.LineNo))
        {
            version.Items.Add(new FormulaVersionItem
            {
                FormulaVersionItemId = Guid.CreateVersion7(),
                FormulaVersionId = version.FormulaVersionId,
                LineNo = material.LineNo,
                ItemType = material.itemType,
                MaterialId = material.MaterialId,
                ProductId = material.ProductId,
                CategoryId = material.CategoryId,
                Quantity = material.Quantity,
                UnitPrice = material.UnitPrice,
                TotalPrice = RoundItemPrice(material.Quantity * material.UnitPrice),
                Unit = material.Unit,
                MaterialExternalIdSnapshot = material.MaterialExternalIdSnapshot,
                MaterialNameSnapshot = material.MaterialNameSnapshot
            });
        }

        return version;
    }

    internal static bool SnapshotMatches(
        FormulaVersion version,
        Formula formula,
        IReadOnlyCollection<FormulaMaterial> activeMaterials)
    {
        if (version.Name != formula.Name ||
            version.Status != formula.Status ||
            version.Note != formula.Note ||
            version.TotalPrice != RoundFormulaPrice(formula.TotalPrice) ||
            version.ProductionPrice != RoundFormulaPrice(formula.ProductionPrice) ||
            version.PresidentPrice != RoundFormulaPrice(formula.PresidentPrice) ||
            version.ProfitMarginPrice != RoundFormulaPrice(formula.ProfitMarginPrice) ||
            version.Items.Count != activeMaterials.Count)
        {
            return false;
        }

        var currentItems = activeMaterials.OrderBy(x => x.LineNo).ToArray();
        var versionItems = version.Items.OrderBy(x => x.LineNo).ToArray();

        return currentItems.Zip(versionItems).All(pair =>
            pair.First.LineNo == pair.Second.LineNo &&
            pair.First.itemType == pair.Second.ItemType &&
            pair.First.MaterialId == pair.Second.MaterialId &&
            pair.First.ProductId == pair.Second.ProductId &&
            pair.First.CategoryId == pair.Second.CategoryId &&
            pair.First.Quantity == pair.Second.Quantity &&
            pair.First.UnitPrice == pair.Second.UnitPrice &&
            RoundItemPrice(pair.First.Quantity * pair.First.UnitPrice) == pair.Second.TotalPrice &&
            pair.First.Unit == pair.Second.Unit &&
            pair.First.MaterialExternalIdSnapshot == pair.Second.MaterialExternalIdSnapshot &&
            pair.First.MaterialNameSnapshot == pair.Second.MaterialNameSnapshot);
    }

    internal static int GetNextVersionNo(FormulaVersion? latestVersion)
        => (latestVersion?.VersionNo ?? 0) + 1;

    internal static void CloseOpenVersions(
        IEnumerable<FormulaVersion> versions,
        DateTime effectiveTo)
    {
        foreach (var version in versions.Where(x => x.EffectiveTo == null))
        {
            version.EffectiveTo = effectiveTo;
        }
    }

    private static void EnsureUniqueLineNumbers(IReadOnlyCollection<FormulaMaterial> activeMaterials)
    {
        if (activeMaterials.GroupBy(x => x.LineNo).Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException("Active formula materials must have unique line numbers.");
        }
    }

    private static string? NormalizeChangeReason(string? changeReason)
    {
        var normalized = string.IsNullOrWhiteSpace(changeReason)
            ? null
            : changeReason.Trim();

        if (normalized?.Length > 500)
        {
            throw new InvalidOperationException("Formula version change reason must not exceed 500 characters.");
        }

        return normalized;
    }

    private static decimal RoundFormulaPrice(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal? RoundFormulaPrice(decimal? value)
        => value.HasValue ? RoundFormulaPrice(value.Value) : null;

    private static decimal RoundItemPrice(decimal value)
        => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
