using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Models;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class ProductPricingSourceValidator
{
    private readonly ProductPricingSourceQueryService _sourceQueryService;
    private readonly IPLMReadDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ProductPricingSourceValidator(
        ProductPricingSourceQueryService sourceQueryService,
        IPLMReadDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _sourceQueryService = sourceQueryService;
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<ProductPricingSourceSnapshot>> ValidateAsync(
        Guid productId,
        Guid companyId,
        string currency,
        ProductPricingSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(sourceType) || sourceId == Guid.Empty)
        {
            return OperationResult<ProductPricingSourceSnapshot>.Fail(
                "Cần chọn loại nguồn giá và nguồn giá hợp lệ.");
        }

        var sourcesByProduct = await _sourceQueryService.LoadAsync(
            [productId],
            companyId,
            currency,
            includeSensitivePricing: true,
            cancellationToken: cancellationToken);
        var source = sourcesByProduct.GetValueOrDefault(productId)?
            .FirstOrDefault(x => x.SourceType == sourceType && x.SourceId == sourceId);
        if (source is null)
        {
            return OperationResult<ProductPricingSourceSnapshot>.Fail(
                await DescribeUnavailableSourceAsync(
                    productId,
                    companyId,
                    sourceType,
                    sourceId,
                    cancellationToken));
        }

        return OperationResult<ProductPricingSourceSnapshot>.Ok(
            new ProductPricingSourceSnapshot(
                source.SourceType,
                source.SourceId,
                source.ExternalId,
                source.Name,
                source.FormulaPricingPolicyId,
                source.FormulaPricingPolicyVersion,
                source.PricingProfile,
                source.CurrentMaterialCost,
                source.IsCurrentMaterialCostComplete,
                source.MissingMaterialPriceCount,
                source.PricingStatus));
    }

    private async Task<string> DescribeUnavailableSourceAsync(
        Guid productId,
        Guid companyId,
        ProductPricingSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken)
        => sourceType switch
        {
            ProductPricingSourceType.Formula => await DescribeFormulaUnavailableAsync(
                productId,
                companyId,
                sourceId,
                cancellationToken),
            ProductPricingSourceType.ManufacturingFormula =>
                await DescribeManufacturingFormulaUnavailableAsync(
                    productId,
                    companyId,
                    sourceId,
                    cancellationToken),
            _ => "Loại nguồn giá đã chọn không được hỗ trợ."
        };

    private async Task<string> DescribeFormulaUnavailableAsync(
        Guid productId,
        Guid companyId,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var source = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.FormulaId == sourceId &&
                x.ProductId == productId &&
                x.Product.CompanyId == companyId)
            .Select(x => new
            {
                x.ExternalId,
                x.IsActive,
                IsProductActive = x.Product.IsActive,
                x.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (source is null)
        {
            return "Không tìm thấy công thức nguồn phù hợp với sản phẩm và công ty hiện tại.";
        }

        if (!source.IsProductActive)
        {
            return $"Không thể dùng công thức [{source.ExternalId}] vì sản phẩm hiện tại đã ngừng hoạt động.";
        }

        if (!source.IsActive)
        {
            return $"Không thể dùng công thức [{source.ExternalId}] vì công thức đã ngừng hoạt động.";
        }

        return $"Không thể dùng công thức [{source.ExternalId}] vì trạng thái hiện tại là " +
               $"“{source.Status}”, chưa đủ điều kiện áp dụng giá.";
    }

    private async Task<string> DescribeManufacturingFormulaUnavailableAsync(
        Guid productId,
        Guid companyId,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var source = await _dbContext.ProductStandardFormulas
            .AsNoTracking()
            .Where(x =>
                x.ProductId == productId &&
                x.CompanyId == companyId &&
                x.ManufacturingFormulaId == sourceId &&
                x.ManufacturingFormula != null &&
                x.ManufacturingFormula.CompanyId == companyId)
            .Select(x => new
            {
                x.ManufacturingFormula!.ExternalId,
                IsProductActive = x.Product.IsActive,
                IsFormulaActive = x.ManufacturingFormula.IsActive,
                x.ManufacturingFormula.Status,
                x.ValidFrom,
                x.ValidTo,
                HasReleasedVersion = x.ManufacturingFormula.ManufacturingFormulaVersions
                    .Any(version =>
                        version.Status == ProductPricingSourceRules.ReleasedManufacturingVersionStatus)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (source is null)
        {
            return "Không tìm thấy công thức sản xuất nguồn được gán cho sản phẩm và công ty hiện tại.";
        }

        if (!source.IsProductActive)
        {
            return $"Không thể dùng công thức sản xuất [{source.ExternalId}] vì sản phẩm hiện tại đã ngừng hoạt động.";
        }

        if (!source.IsFormulaActive)
        {
            return $"Không thể dùng công thức sản xuất [{source.ExternalId}] vì công thức đã ngừng hoạt động.";
        }

        var now = _dateTimeProvider.Now;
        if (source.ValidFrom > now)
        {
            return $"Không thể dùng công thức sản xuất [{source.ExternalId}] vì chỉ có hiệu lực từ " +
                   $"{source.ValidFrom:dd/MM/yyyy}.";
        }

        if (source.ValidTo is { } validTo && validTo < now)
        {
            return $"Không thể dùng công thức sản xuất [{source.ExternalId}] vì đã hết hiệu lực từ " +
                   $"{validTo:dd/MM/yyyy}.";
        }

        if (!ProductPricingSourceRules.EligibleManufacturingFormulaStatuses.Contains(source.Status) &&
            !source.HasReleasedVersion)
        {
            return $"Không thể dùng công thức sản xuất [{source.ExternalId}] vì trạng thái hiện tại là " +
                   $"“{source.Status}” và chưa có phiên bản được phát hành.";
        }

        return $"Không thể dùng công thức sản xuất [{source.ExternalId}] vì chưa đủ điều kiện áp dụng giá.";
    }
}

internal sealed record ProductPricingSourceSnapshot(
    ProductPricingSourceType SourceType,
    Guid SourceId,
    string ExternalId,
    string Name,
    Guid? FormulaPricingPolicyId,
    int? FormulaPricingPolicyVersion,
    HRM.Domain.Enums.Formulas.FormulaPricingProfile? PricingProfile,
    decimal? MaterialCostSnapshot,
    bool IsMaterialCostComplete,
    int MissingMaterialPriceCount,
    string PricingStatus)
{
    public Guid? FormulaId => SourceType == ProductPricingSourceType.Formula
        ? SourceId
        : null;

    public Guid? ManufacturingFormulaId =>
        SourceType == ProductPricingSourceType.ManufacturingFormula
            ? SourceId
            : null;
}
