using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricingOptions.Models;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricingOptions;

/// <summary>
/// Tổng hợp sản phẩm đã hoàn tất phát triển, công thức được chọn và giá nguồn mới nhất
/// để sale tra cứu trước khi tạo báo giá.
/// </summary>
internal sealed class GetQuotationProductPricingOptionsQueryHandler
    : IRequestHandler<GetQuotationProductPricingOptionsQuery,
        OperationResult<PagedResult<QuotationProductPricingOptionDto>>>
{
    private const int MaxRequestTypeLength = 100;

    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMaterialPriceQueryService _materialPriceQueryService;

    public GetQuotationProductPricingOptionsQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        IMaterialPriceQueryService materialPriceQueryService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _materialPriceQueryService = materialPriceQueryService;
    }

    public async Task<OperationResult<PagedResult<QuotationProductPricingOptionDto>>> Handle(
        GetQuotationProductPricingOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<PagedResult<QuotationProductPricingOptionDto>>.Fail(
                "Current user does not have a company context.");
        }

        if (request.NormalizedRequestType.Length > MaxRequestTypeLength)
        {
            return OperationResult<PagedResult<QuotationProductPricingOptionDto>>.Fail(
                $"RequestType cannot exceed {MaxRequestTypeLength} characters.");
        }

        if (request.QuotationStatus.HasValue && !Enum.IsDefined(request.QuotationStatus.Value))
        {
            return OperationResult<PagedResult<QuotationProductPricingOptionDto>>.Fail(
                "QuotationStatus is invalid.");
        }

        if (request.QuotationId == Guid.Empty)
        {
            return OperationResult<PagedResult<QuotationProductPricingOptionDto>>.Fail(
                "QuotationId is invalid.");
        }

        var sampleRequestStatus =
            (request.Status ?? SampleRequestStatus.Completed).ToString();

        var requestType = request.NormalizedRequestType;
        var canViewSensitivePricing =
            _currentUser.IsInRole(ApplicationRoles.President) ||
            _currentUser.IsInRole(ApplicationRoles.Developer);

        var eligibleRequests = _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == companyId &&
                x.Status == sampleRequestStatus &&
                x.RequestType == requestType &&
                x.Product.IsActive &&
                x.Product.CompanyId == companyId);

        var eligibleProductIds = eligibleRequests
            .Select(x => x.ProductId)
            .Distinct();

        var latestRequestQuery = eligibleProductIds
            .SelectMany(productId => eligibleRequests
                .Where(x => x.ProductId == productId)
                .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                .ThenByDescending(x => x.SampleRequestId)
                .Take(1));

        if (request.QuotationStatus is { } quotationStatus)
        {
            latestRequestQuery = latestRequestQuery.Where(x =>
                _dbContext.QuotationLines.Any(line =>
                    line.ProductId == x.ProductId &&
                    line.Quotation.IsActive &&
                    line.Quotation.CompanyId == companyId &&
                    line.Quotation.Status == quotationStatus));
        }

        if (request.QuotationId is { } quotationId)
        {
            latestRequestQuery = latestRequestQuery.Where(x =>
                _dbContext.QuotationLines.Any(line =>
                    line.QuotationId == quotationId &&
                    line.ProductId == x.ProductId &&
                    line.Quotation.IsActive &&
                    line.Quotation.CompanyId == companyId));
        }

        if (request.NormalizedKeyword is { } keyword)
        {
            if (IsQuotationExternalIdKeyword(keyword))
            {
                latestRequestQuery = latestRequestQuery.Where(x =>
                    _dbContext.QuotationLines.Any(line =>
                        line.ProductId == x.ProductId &&
                        line.Quotation.IsActive &&
                        line.Quotation.CompanyId == companyId &&
                        line.Quotation.ExternalId.StartsWith(keyword)));
            }
            else if (IsSampleRequestExternalIdKeyword(keyword))
            {
                latestRequestQuery = latestRequestQuery.Where(x =>
                    eligibleRequests.Any(sampleRequest =>
                        sampleRequest.ProductId == x.ProductId &&
                        sampleRequest.ExternalId.StartsWith(keyword)));
            }
            else if (IsFormulaExternalIdKeyword(keyword))
            {
                latestRequestQuery = latestRequestQuery.Where(x =>
                    _dbContext.Formulas.Any(formula =>
                        formula.IsActive &&
                        formula.CompanyId == companyId &&
                        formula.ProductId == x.ProductId &&
                        formula.ExternalId.StartsWith(keyword)));
            }
            else
            {
                latestRequestQuery = latestRequestQuery.Where(x =>
                    (x.Product.ColourCode ?? string.Empty).Contains(keyword) ||
                    (x.Product.Name ?? string.Empty).Contains(keyword) ||
                    eligibleRequests.Any(sampleRequest =>
                        sampleRequest.ProductId == x.ProductId &&
                        sampleRequest.ExternalId.Contains(keyword)) ||
                    _dbContext.Formulas.Any(formula =>
                        formula.IsActive &&
                        formula.CompanyId == companyId &&
                        formula.ProductId == x.ProductId &&
                        (formula.ExternalId.Contains(keyword) || formula.Name.Contains(keyword))));
            }
        }

        var totalCount = await latestRequestQuery.CountAsync(cancellationToken);
        latestRequestQuery = ApplySorting(latestRequestQuery, request);

        var productCandidates = await latestRequestQuery
            .Select(x => new PricingProductCandidate
            {
                SampleRequestId = x.SampleRequestId,
                SampleRequestExternalId = x.ExternalId,
                CompletedDate = x.UpdatedDate ?? x.CreatedDate,
                ProductId = x.ProductId,
                ProductCode = x.Product.ColourCode ?? string.Empty,
                ProductName = x.Product.Name ?? string.Empty,

                CustomerId = x.Customer.CustomerId,
                CustomerExternalId =x.Customer.ExternalId ?? string.Empty,
                CustomerName = x.Customer.CustomerName ?? string.Empty,
            })
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        if (productCandidates.Count == 0)
        {
            return OperationResult<PagedResult<QuotationProductPricingOptionDto>>.Ok(
                new PagedResult<QuotationProductPricingOptionDto>(
                    [],
                    totalCount,
                    request.NormalizedPageNumber,
                    request.NormalizedPageSize));
        }

        var productIds = productCandidates
            .Select(x => x.ProductId)
            .ToList();

        var formulaRows = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId))
            .Select(x => new PricingFormula
            {
                ProductId = x.ProductId,
                ProductCode = x.Product.ColourCode ?? x.Product.Code ?? string.Empty,
                ProductAdditive = x.Product.Additive,
                FormulaId = x.FormulaId,
                FormulaExternalId = x.ExternalId,
                FormulaName = x.Name,
                MaterialCost = x.TotalPrice,
                ManufacturingCost = x.ProductionPrice,
                StandardSellingPrice = x.PresidentPrice,
                IsSelected = x.IsSelect,
                PricingUpdatedDate = canViewSensitivePricing ? x.UpdatedDate : null
            })
            .OrderByDescending(x => x.IsSelected)
            .ThenBy(x => x.FormulaExternalId)
            .ToListAsync(cancellationToken);

        var formulaIds = formulaRows.Select(x => x.FormulaId).ToList();

        var materialRows = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                formulaIds.Contains(x.FormulaId) &&
                x.Formula.IsActive &&
                x.Formula.CompanyId == companyId)
            .Select(x => new PricingMaterial
            {
                FormulaId = x.FormulaId,
                FormulaMaterialId = x.FormulaMaterialId,
                ItemId = x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure
                    ? x.Material != null && x.Material.CompanyId == companyId
                        ? x.MaterialId
                        : null
                    : x.Product != null && x.Product.CompanyId == companyId
                        ? x.ProductId
                        : null,
                ItemType = x.itemType,
                ItemCode = x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure
                    ? x.Material != null
                        ? x.Material.ExternalId ?? x.MaterialExternalIdSnapshot ?? string.Empty
                        : x.MaterialExternalIdSnapshot ?? string.Empty
                    : x.Product != null
                        ? x.Product.ColourCode ?? x.MaterialExternalIdSnapshot ?? string.Empty
                        : "-",
                ItemName = x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure
                    ? x.Material != null
                        ? x.Material.Name ?? x.MaterialNameSnapshot ?? string.Empty
                        : x.MaterialNameSnapshot ?? string.Empty
                    : x.Product != null
                        ? x.Product.Name ?? x.MaterialNameSnapshot ?? string.Empty
                        : "-",
                Quantity = x.Quantity,
                Unit = x.Unit ?? string.Empty,
                LineNo = x.LineNo
            })
            .OrderBy(x => x.FormulaId)
            .ThenBy(x => x.LineNo)
            .ToListAsync(cancellationToken);

        var priceRequests = materialRows
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .Select(x => new PriceItemRequest
            {
                ItemType = NormalizePriceItemType(x.ItemType),
                MaterialId = IsMaterial(x.ItemType) ? x.ItemId : null,
                ProductId = IsMaterial(x.ItemType) ? null : x.ItemId
            })
            .ToList();

        var latestPriceByItem = await _materialPriceQueryService
            .LoadLatestItemPriceInfoDictAsync(priceRequests, cancellationToken);

        var realtimeMaterialCostByFormula = materialRows
            .GroupBy(x => x.FormulaId)
            .ToDictionary(
                x => x.Key,
                x => FormulaRealtimeMaterialCostCalculator.Calculate(
                    x.Select(y => new FormulaMaterialCostItem(
                        y.ItemId,
                        y.ItemType,
                        y.Quantity)),
                    latestPriceByItem));

        IReadOnlyDictionary<Guid, IReadOnlyList<QuotationProductPricingMaterialSupplierDto>>
            supplierPricesByMaterial =
                new Dictionary<Guid, IReadOnlyList<QuotationProductPricingMaterialSupplierDto>>();

        if (canViewSensitivePricing)
        {
            var materialIds = materialRows
                .Where(x => IsMaterial(x.ItemType) && x.ItemId.HasValue)
                .Select(x => x.ItemId!.Value)
                .Distinct()
                .ToList();

            var supplierPriceRows = await _dbContext.MaterialsSuppliers
                .AsNoTracking()
                .Where(x =>
                    x.IsActive == true &&
                    materialIds.Contains(x.MaterialId) &&
                    x.Material.IsActive == true &&
                    x.Material.CompanyId == companyId &&
                    x.Supplier.IsActive == true &&
                    x.Supplier.CompanyId == companyId)
                .OrderByDescending(x => x.IsPreferred)
                .ThenBy(x => x.Supplier.SupplierName)
                .Select(x => new PricingMaterialSupplier
                {
                    MaterialId = x.MaterialId,
                    MaterialsSupplierId = x.MaterialsSuppliersId,
                    SupplierId = x.SupplierId,
                    SupplierCode = x.Supplier.ExternalId ?? string.Empty,
                    SupplierName = x.Supplier.SupplierName ?? string.Empty,
                    CurrentPrice = x.CurrentPrice,
                    Currency = x.Currency,
                    IsPreferred = x.IsPreferred == true,
                    UpdatedDate = x.UpdatedDate
                })
                .ToListAsync(cancellationToken);

            supplierPricesByMaterial = supplierPriceRows
                .GroupBy(x => x.MaterialId)
                .ToDictionary(
                    x => x.Key,
                    x => (IReadOnlyList<QuotationProductPricingMaterialSupplierDto>)x
                        .Select(y => new QuotationProductPricingMaterialSupplierDto
                        {
                            MaterialsSupplierId = y.MaterialsSupplierId,
                            SupplierId = y.SupplierId,
                            SupplierCode = y.SupplierCode,
                            SupplierName = y.SupplierName,
                            CurrentPrice = y.CurrentPrice,
                            Currency = y.Currency,
                            IsPreferred = y.IsPreferred,
                            UpdatedDate = y.UpdatedDate
                        })
                        .ToList());
        }

        IReadOnlyDictionary<Guid, IReadOnlyList<QuotationProductPricingMaterialDto>>
            materialsByFormula = canViewSensitivePricing
                ? materialRows
                    .GroupBy(x => x.FormulaId)
                    .ToDictionary(
                        x => x.Key,
                        x => (IReadOnlyList<QuotationProductPricingMaterialDto>)x
                            .OrderBy(y => y.LineNo)
                            .Select(y => MapMaterial(
                                y,
                                latestPriceByItem,
                                supplierPricesByMaterial))
                            .ToList())
                : new Dictionary<Guid, IReadOnlyList<QuotationProductPricingMaterialDto>>();

        var formulasByProduct = formulaRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<QuotationProductPricingFormulaDto>)x
                    .Select(formula => MapFormula(
                        formula,
                        realtimeMaterialCostByFormula.GetValueOrDefault(formula.FormulaId)
                            ?? new FormulaRealtimeMaterialCostResult(null, false, 0),
                        materialsByFormula.GetValueOrDefault(formula.FormulaId) ?? [],
                        canViewSensitivePricing))
                    .ToList());

        var items = productCandidates
            .Select(product => new QuotationProductPricingOptionDto
            {
                SampleRequestId = product.SampleRequestId,
                SampleRequestExternalId = product.SampleRequestExternalId,
                CompletedDate = product.CompletedDate,
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,

                CustomerId = product.CustomerId,
                CustomerExternalId = product.CustomerExternalId,
                CustomerName = product.CustomerName,
                Formulas = formulasByProduct.GetValueOrDefault(product.ProductId) ?? []
            })
            .ToList();

        return OperationResult<PagedResult<QuotationProductPricingOptionDto>>.Ok(
            new PagedResult<QuotationProductPricingOptionDto>(
                items,
                totalCount,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));
    }

    private static IQueryable<SampleRequest> ApplySorting(
        IQueryable<SampleRequest> query,
        GetQuotationProductPricingOptionsQuery request)
    {
        return request.NormalizedSortBy?.ToLowerInvariant() switch
        {
            QuotationProductPricingSortFields.ExternalId => request.SortDescending
                ? query
                    .OrderByDescending(x => x.ExternalId)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.SampleRequestId)
                : query
                    .OrderBy(x => x.ExternalId)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.SampleRequestId),

            QuotationProductPricingSortFields.ColourCode => request.SortDescending
                ? query
                    .OrderByDescending(x => x.Product.ColourCode)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.SampleRequestId)
                : query
                    .OrderBy(x => x.Product.ColourCode)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.SampleRequestId),

            QuotationProductPricingSortFields.ProductName => request.SortDescending
                ? query
                    .OrderByDescending(x => x.Product.Name)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.SampleRequestId)
                : query
                    .OrderBy(x => x.Product.Name)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.SampleRequestId),

            QuotationProductPricingSortFields.UpdatedDate => request.SortDescending
                ? query
                    .OrderByDescending(x => x.UpdatedDate)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.SampleRequestId)
                : query
                    .OrderBy(x => x.UpdatedDate)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.SampleRequestId),

            QuotationProductPricingSortFields.CreatedDate => request.SortDescending
                ? query
                    .OrderByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.UpdatedDate)
                    .ThenByDescending(x => x.SampleRequestId)
                : query
                    .OrderBy(x => x.CreatedDate)
                    .ThenByDescending(x => x.UpdatedDate)
                    .ThenByDescending(x => x.SampleRequestId),

            _ => query
                .OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.SampleRequestId)
        };
    }

    private static QuotationProductPricingMaterialDto MapMaterial(
        PricingMaterial material,
        IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> latestPriceByItem,
        IReadOnlyDictionary<Guid, IReadOnlyList<QuotationProductPricingMaterialSupplierDto>> supplierPricesByMaterial)
    {
        var normalizedType = NormalizePriceItemType(material.ItemType);
        LatestItemPriceDto? latestPrice = null;
        var hasPrice = material.ItemId.HasValue &&
            latestPriceByItem.TryGetValue(
                new PriceItemKey(normalizedType, material.ItemId.Value),
                out latestPrice) &&
            latestPrice.PriceSource != LatestPriceSourceType.Unknown;

        return new QuotationProductPricingMaterialDto
        {
            FormulaMaterialId = material.FormulaMaterialId,
            ItemId = material.ItemId,
            ItemType = material.ItemType,
            ItemCode = material.ItemCode,
            ItemName = material.ItemName,
            Quantity = material.Quantity,
            Unit = material.Unit,
            HasLatestPrice = hasPrice,
            LatestUnitPrice = hasPrice ? latestPrice!.CurrentPrice : null,
            LatestTotalPrice = hasPrice ? material.Quantity * latestPrice!.CurrentPrice : null,
            LatestPriceDate = hasPrice ? latestPrice!.PriceDate : null,
            LatestPriceSource = hasPrice
                ? latestPrice!.PriceSource
                : LatestPriceSourceType.Unknown,
            SupplierPrices = IsMaterial(material.ItemType) && material.ItemId.HasValue
                ? supplierPricesByMaterial.GetValueOrDefault(material.ItemId.Value) ?? []
                : []
        };
    }

    private static QuotationProductPricingFormulaDto MapFormula(
        PricingFormula formula,
        FormulaRealtimeMaterialCostResult realtimeMaterialCost,
        IReadOnlyList<QuotationProductPricingMaterialDto> materials,
        bool canViewSensitivePricing)
    {
        var pricing = realtimeMaterialCost.IsComplete &&
                      realtimeMaterialCost.MaterialCost.HasValue
            ? FormulaPriceCalculator.Calculate(
                formula.ProductCode,
                formula.ProductAdditive,
                realtimeMaterialCost.MaterialCost.Value,
                formula.ManufacturingCost,
                formula.StandardSellingPrice)
            : null;

        var manufacturingCost = pricing?.ManufacturingCost ??
            FormulaPriceCalculator.ResolveManufacturingCost(
                formula.ProductCode,
                formula.ProductAdditive,
                formula.ManufacturingCost);
        var standardSellingPrice =
            pricing?.StandardSellingPrice ?? formula.StandardSellingPrice;

        if (!canViewSensitivePricing)
        {
            return new QuotationProductPricingFormulaDto
            {
                FormulaId = formula.FormulaId,
                FormulaExternalId = formula.FormulaExternalId,
                FormulaName = formula.FormulaName,
                IsCustomerSelected = formula.IsSelected,
                StandardSellingPrice = standardSellingPrice
            };
        }

        return new QuotationProductPricingFormulaDto
        {
            FormulaId = formula.FormulaId,
            FormulaExternalId = formula.FormulaExternalId,
            FormulaName = formula.FormulaName,
            IsCustomerSelected = formula.IsSelected,
            MaterialCost = formula.MaterialCost,
            RealtimeMaterialCost = realtimeMaterialCost.MaterialCost,
            IsRealtimeMaterialCostComplete = realtimeMaterialCost.IsComplete,
            MissingMaterialPriceCount = realtimeMaterialCost.MissingPriceCount,
            ManufacturingCost = manufacturingCost,
            StandardSellingPrice = standardSellingPrice,
            ProfitMarginRate = pricing?.ProfitMarginRate,
            PricingUpdatedDate = formula.PricingUpdatedDate,
            Pricing = pricing,
            Materials = materials
        };
    }

    private static bool IsMaterial(ItemType itemType)
    {
        return itemType is ItemType.Material or ItemType.MaterialFailure;
    }

    private static ItemType NormalizePriceItemType(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.MaterialFailure => ItemType.Material,
            ItemType.ProductFailure => ItemType.Product,
            _ => itemType
        };
    }

    private static bool IsQuotationExternalIdKeyword(string keyword)
        => keyword.StartsWith(
            DocumentPrefix.BBG.ToString(),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsSampleRequestExternalIdKeyword(string keyword)
        => keyword.StartsWith(
            BuildGlobalExternalIdPrefix(DocumentPrefix.TP),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsFormulaExternalIdKeyword(string keyword)
        => keyword.StartsWith(
            DocumentPrefix.VU.ToString(),
            StringComparison.OrdinalIgnoreCase);

    private static string BuildGlobalExternalIdPrefix(DocumentPrefix prefix)
        => $"{prefix}_";
}
