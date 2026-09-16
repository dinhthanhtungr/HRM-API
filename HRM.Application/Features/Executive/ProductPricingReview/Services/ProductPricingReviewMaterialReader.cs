using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Pricing.Rules;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Executive.ProductPricingReview.Services;

internal sealed class ProductPricingReviewMaterialReader
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMaterialPriceQueryService _materialPriceQueryService;
    private readonly IPriceReadDbContext _priceReadDbContext;

    public ProductPricingReviewMaterialReader(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        IMaterialPriceQueryService materialPriceQueryService,
        IPriceReadDbContext priceReadDbContext)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _materialPriceQueryService = materialPriceQueryService;
        _priceReadDbContext = priceReadDbContext;
    }

    public async Task<OperationResult<PricingReviewSupplierPricesDto>> GetSupplierPricesAsync(
        Guid productId,
        Guid materialId,
        string? currency,
        CancellationToken cancellationToken)
    {
        var access = await ValidateMaterialAccessAsync(productId, materialId, cancellationToken);
        if (!access.Success || access.Data is null)
            return OperationResult<PricingReviewSupplierPricesDto>.Fail(access.Message!);
        var normalizedCurrency = ProductPricingReviewRules.NormalizeCurrency(currency);
        if (normalizedCurrency is null)
            return OperationResult<PricingReviewSupplierPricesDto>.Fail("Only VND is supported.");

        var companyId = ProductPricingReviewRules.GetContext(_currentUser).Data.CompanyId;
        var allSupplierPrices = await _dbContext.MaterialsSuppliers.AsNoTracking()
            .Where(x => x.MaterialId == materialId && x.IsActive == true &&
                        x.Supplier.CompanyId == companyId && x.Supplier.IsActive == true)
            .Select(x => new SupplierPriceRow(
                x.MaterialsSuppliersId,
                x.SupplierId,
                x.Supplier.ExternalId ?? string.Empty,
                x.Supplier.SupplierName ?? string.Empty,
                x.CurrentPrice,
                x.Currency ?? "VND",
                x.UpdatedDate ?? x.CreateDate,
                x.IsPreferred == true))
            .ToListAsync(cancellationToken);

        var latestPrices = await _materialPriceQueryService.LoadLatestMaterialPriceInfoDictAsync(
            [materialId], cancellationToken);

        var selectedSupplierId = latestPrices.TryGetValue(materialId, out var latestPrice)
            ? latestPrice.PriceSource == MaterialPriceSource.MaterialSupplier
                ? allSupplierPrices
                .OrderByDescending(x => x.UpdatedAt)
                .ThenByDescending(x => x.IsPreferred)
                .Select(x => (Guid?)x.SupplierId)
                .FirstOrDefault()
                : latestPrice.PriceSource == MaterialPriceSource.PurchaseOrder
                    ? await GetLatestPurchaseOrderSupplierIdAsync(materialId, cancellationToken)
                    : null
            : null;

        var suppliers = allSupplierPrices
            .Where(x => string.Equals(x.Currency, normalizedCurrency, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.IsPreferred)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(x => new PricingReviewSupplierPriceDto
            {
                SupplierPriceId = x.SupplierPriceId,
                SupplierId = x.SupplierId,
                SupplierCode = x.SupplierCode,
                SupplierName = x.SupplierName,
                UnitPrice = x.UnitPrice,
                Currency = x.Currency,
                PriceDate = x.UpdatedAt,
                // Chỉ đánh dấu NCC khi canonical latest-price selector chọn giá NCC,
                // không đánh dấu nếu PO mới hơn hoặc có cùng ngày.
                IsCurrentStandardPrice = x.SupplierId == selectedSupplierId,
                UpdatedAt = x.UpdatedAt
            })
            .ToList();

        return OperationResult<PricingReviewSupplierPricesDto>.Ok(new PricingReviewSupplierPricesDto
        {
            MaterialId = access.Data.MaterialId,
            MaterialCode = access.Data.Code,
            MaterialName = access.Data.Name,
            Unit = access.Data.Unit,
            Suppliers = suppliers
        });
    }

    public async Task<OperationResult<PagedResult<PricingReviewMaterialPriceHistoryDto>>> GetPriceHistoryAsync(
        Guid productId,
        Guid materialId,
        string? currency,
        int pageNumber,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var access = await ValidateMaterialAccessAsync(productId, materialId, cancellationToken);
        if (!access.Success)
            return OperationResult<PagedResult<PricingReviewMaterialPriceHistoryDto>>.Fail(access.Message!);
        var normalizedCurrency = ProductPricingReviewRules.NormalizeCurrency(currency);
        if (normalizedCurrency is null)
            return OperationResult<PagedResult<PricingReviewMaterialPriceHistoryDto>>.Fail("Only VND is supported.");

        var companyId = ProductPricingReviewRules.GetContext(_currentUser).Data.CompanyId;
        var query = _dbContext.PriceHistories.AsNoTracking()
            .Where(x => x.MaterialsSuppliers.MaterialId == materialId &&
                        x.MaterialsSuppliers.Supplier.CompanyId == companyId &&
                        (x.Currency ?? x.MaterialsSuppliers.Currency ?? "VND").ToUpper() == normalizedCurrency);
        var total = await query.CountAsync(cancellationToken);
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("suppliername", false) => query.OrderBy(x => x.MaterialsSuppliers.Supplier.SupplierName),
            ("suppliername", true) => query.OrderByDescending(x => x.MaterialsSuppliers.Supplier.SupplierName),
            ("unitprice", false) => query.OrderBy(x => x.OldPrice),
            ("unitprice", true) => query.OrderByDescending(x => x.OldPrice),
            ("recordedat", false) => query.OrderBy(x => x.CreateDate),
            _ => query.OrderByDescending(x => x.CreateDate)
        };
        var page = ProductPricingReviewRules.NormalizePageNumber(pageNumber);
        var size = ProductPricingReviewRules.NormalizePageSize(pageSize);
        var rows = await query.Skip((page - 1) * size).Take(size)
            .Select(x => new PricingReviewMaterialPriceHistoryDto
            {
                PriceHistoryId = x.PriceHistoryId,
                SupplierPriceId = x.MaterialsSuppliersId,
                SupplierId = x.MaterialsSuppliers.SupplierId,
                SupplierCode = x.MaterialsSuppliers.Supplier.ExternalId ?? string.Empty,
                SupplierName = x.MaterialsSuppliers.Supplier.SupplierName ?? string.Empty,
                UnitPrice = x.OldPrice,
                Currency = x.Currency ?? x.MaterialsSuppliers.Currency ?? "VND",
                RecordedAt = x.CreateDate,
                RecordedByEmployeeId = x.CreatedBy,
                RecordedByName = x.CreatedByNavigation != null ? x.CreatedByNavigation.FullName : null
            }).ToListAsync(cancellationToken);
        return OperationResult<PagedResult<PricingReviewMaterialPriceHistoryDto>>.Ok(
            new PagedResult<PricingReviewMaterialPriceHistoryDto>(rows, total, page, size));
    }

    private async Task<OperationResult<MaterialAccessRow>> ValidateMaterialAccessAsync(
        Guid productId,
        Guid materialId,
        CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<MaterialAccessRow>.Fail(context.Message!);
        if (productId == Guid.Empty || materialId == Guid.Empty)
            return OperationResult<MaterialAccessRow>.Fail("ProductId and MaterialId are required.");

        var companyId = context.Data.CompanyId;
        var material = await _dbContext.Materials.AsNoTracking()
            .Where(x => x.MaterialId == materialId && x.CompanyId == companyId && x.IsActive == true)
            .Select(x => new MaterialAccessRow(
                x.MaterialId,
                x.ExternalId ?? x.CustomCode ?? string.Empty,
                x.Name ?? string.Empty,
                x.Unit ?? string.Empty))
            .FirstOrDefaultAsync(cancellationToken);
        if (material is null)
            return OperationResult<MaterialAccessRow>.Fail(
                "Material was not found, inactive, or outside the current company.");

        var belongsToProduct = await _dbContext.FormulaMaterials.AsNoTracking()
                .AnyAsync(x => x.MaterialId == materialId && x.IsActive &&
                               x.Formula.ProductId == productId && x.Formula.IsActive &&
                               x.Formula.CompanyId == companyId, cancellationToken) ||
            await _dbContext.ManufacturingFormulaMaterials.AsNoTracking()
                .AnyAsync(x => x.MaterialId == materialId && x.IsActive &&
                               x.ManufacturingFormula.CompanyId == companyId &&
                               x.ManufacturingFormula.IsActive &&
                               (x.ManufacturingFormula.ProductStandardFormulas.Any(link =>
                                    link.ProductId == productId &&
                                    link.CompanyId == companyId &&
                                    link.Product.IsActive &&
                                    link.Product.CompanyId == companyId) ||
                                x.ManufacturingFormula.ProductionSelectVersions.Any(version =>
                                    version.CompanyId == companyId &&
                                    version.MfgProductionOrder.CompanyId == companyId &&
                                    version.MfgProductionOrder.IsActive &&
                                    version.MfgProductionOrder.ProductId == productId &&
                                    version.MfgProductionOrder.Product.IsActive &&
                                    version.MfgProductionOrder.Product.CompanyId == companyId)),
                    cancellationToken);
        return belongsToProduct
            ? OperationResult<MaterialAccessRow>.Ok(material)
            : OperationResult<MaterialAccessRow>.Fail(
                "Material is not part of an active VU/VA source assigned to this product.");
    }

    private async Task<Guid?> GetLatestPurchaseOrderSupplierIdAsync(
        Guid materialId,
        CancellationToken cancellationToken)
        => await _priceReadDbContext.PurchaseOrderDetails
            .AsNoTracking()
            .Where(x => x.IsActive &&
                        x.MaterialId == materialId &&
                        x.PurchaseOrder != null &&
                        (x.PurchaseOrder.IsActive ?? true) &&
                        (x.PurchaseOrder.Status == null ||
                         !PurchaseOrderPriceRules.CanceledStatuses.Contains(x.PurchaseOrder.Status)))
            .OrderByDescending(x => x.PurchaseOrder!.CreateDate)
            .ThenByDescending(x => x.LineNo)
            .Select(x => x.PurchaseOrder!.SupplierId)
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record MaterialAccessRow(Guid MaterialId, string Code, string Name, string Unit);

    private sealed record SupplierPriceRow(
        Guid SupplierPriceId,
        Guid SupplierId,
        string SupplierCode,
        string SupplierName,
        decimal? UnitPrice,
        string Currency,
        DateTime? UpdatedAt,
        bool IsPreferred);
}
