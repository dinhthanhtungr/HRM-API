using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Features.Executive.ProductPricingReview.Services;

internal static class ProductPricingReviewRules
{
    public const int MaximumPageSize = 100;
    public const int DefaultPageSize = 20;
    public const int StalePriceDays = 30;

    // Executive source switcher only exposes VU formulas that are far enough in the
    // PLM workflow to be used for pricing review. PendingSaleConfirmation is the
    // persisted status used when Lab requests a formula update from Sale.
    public static readonly string[] EligibleVuFormulaStatuses =
    [
        FormulaStatus.Approved.ToString(),
        FormulaStatus.Completed.ToString(),
        FormulaStatus.SampleSent.ToString(),
        FormulaStatus.PendingSaleConfirmation.ToString()
    ];

    public static bool IsEligibleVuFormulaStatus(string? status)
        => status is not null && EligibleVuFormulaStatuses.Contains(status);

    public static OperationResult<(Guid CompanyId, Guid EmployeeId)> GetContext(ICurrentUser currentUser)
    {
        if (!ProductPricingAccessRules.CanManage(currentUser))
        {
            return OperationResult<(Guid, Guid)>.Fail(
                "Only President or Developer can access executive product pricing review.");
        }

        if (currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<(Guid, Guid)>.Fail(
                "Current company and employee context are required.");
        }

        return OperationResult<(Guid, Guid)>.Ok((companyId, employeeId));
    }

    public static string? NormalizeCurrency(string? currency)
    {
        var normalized = string.IsNullOrWhiteSpace(currency)
            ? "VND"
            : currency.Trim().ToUpperInvariant();
        return normalized == "VND" ? normalized : null;
    }

    public static ProductPricingSourceType ToLegacy(PricingReviewSourceType type)
        => type == PricingReviewSourceType.VA
            ? ProductPricingSourceType.ManufacturingFormula
            : ProductPricingSourceType.Formula;

    public static PricingReviewSourceType ToPublic(ProductPricingSourceType type)
        => type == ProductPricingSourceType.ManufacturingFormula
            ? PricingReviewSourceType.VA
            : PricingReviewSourceType.VU;

    public static string BuildSourceDisplayName(
        PricingReviewSourceType sourceType,
        string sourceCode,
        string? sourceName)
        => sourceType == PricingReviewSourceType.VA || string.IsNullOrWhiteSpace(sourceName)
            ? sourceCode
            : sourceCode + " · " + sourceName;

    /// <summary>
    /// Chọn công thức gợi ý theo mốc sự kiện nghiệp vụ do từng loại nguồn cung cấp,
    /// không theo ngày tạo chung của Formula/ManufacturingFormula.
    /// </summary>
    public static PricingReviewCurrentFormulaUseDto? SelectLatestSuggestedFormulaUse(
        params PricingReviewFormulaUseCandidate?[] candidates)
        => candidates
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.PriorityAt)
            .ThenByDescending(x => x.FormulaUse.SourceId)
            .Select(x => x.FormulaUse)
            .FirstOrDefault();

    public static ProductPricingChangedField? ToLegacy(PricingReviewChangedField? field)
        => field switch
        {
            PricingReviewChangedField.ManufacturingCost => ProductPricingChangedField.ManufacturingCost,
            PricingReviewChangedField.StandardSellingPrice => ProductPricingChangedField.StandardSellingPrice,
            PricingReviewChangedField.ProfitMarginPercent => ProductPricingChangedField.ProfitMarginRate,
            null => null,
            _ => null
        };

    public static int NormalizePageNumber(int pageNumber) => Math.Max(1, pageNumber);

    public static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaximumPageSize);
}

/// <summary>Một công thức gợi ý cùng mốc nghiệp vụ được dùng để xếp hạng.</summary>
internal sealed record PricingReviewFormulaUseCandidate(
    PricingReviewCurrentFormulaUseDto FormulaUse,
    DateTime PriorityAt);
