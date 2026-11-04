using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.MigrateProductCategories;

/// <summary>
/// Reclassifies active products across companies using the approved legacy-data rules.
/// </summary>
public sealed class MigrateProductCategoriesCommand : IRequest<OperationResult<ProductCategoryMigrationResult>>
{
    /// <summary>
    /// Defaults to preview-only. Database changes require an explicit false value.
    /// </summary>
    public bool DryRun { get; init; } = true;

    /// <summary>
    /// Optional canonical target filter: CMP, CMB, AMB, ADD, or PIG.
    /// </summary>
    public string? TargetCategoryCode { get; init; }
}

public sealed class ProductCategoryMigrationResult
{
    public bool DryRun { get; init; }
    public string? TargetCategoryCode { get; init; }
    public int CompoundToCmpCount { get; init; }
    public int ColorMasterbatchToCmbCount { get; init; }
    public int AdditiveMasterbatchToAmbCount { get; init; }
    public int AdditiveToAddCount { get; init; }
    public int PigmentToPigCount { get; init; }
    public IReadOnlyList<ProductCategoryMigrationPreviewItem> PreviewItems { get; init; } = [];
    public bool HasMorePreviewItems { get; init; }

    public int TotalMigratedCount =>
        CompoundToCmpCount + ColorMasterbatchToCmbCount + AdditiveMasterbatchToAmbCount + AdditiveToAddCount + PigmentToPigCount;
}

public sealed class ProductCategoryMigrationPreviewItem
{
    public Guid ProductId { get; init; }
    public string? ColourCode { get; init; }
    public string? ProductName { get; init; }
    public string? CurrentCategoryName { get; init; }
    public string TargetCategoryCode { get; init; } = string.Empty;
    public string MatchedBy { get; init; } = string.Empty;
}
