using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
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
    private readonly ICRMReadDbContext _crmDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMaterialPriceQueryService _materialPriceQueryService;
    private readonly ProductPricingSourceQueryService _sourceQueryService;

    public GetQuotationProductPricingOptionsQueryHandler(
        IPLMReadDbContext dbContext,
        ICRMReadDbContext crmDbContext,
        ICurrentUser currentUser,
        IMaterialPriceQueryService materialPriceQueryService,
        ProductPricingSourceQueryService sourceQueryService)
    {
        _dbContext = dbContext;
        _crmDbContext = crmDbContext;
        _currentUser = currentUser;
        _materialPriceQueryService = materialPriceQueryService;
        _sourceQueryService = sourceQueryService;
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

        if (request.NormalizedRequestType is { Length: > MaxRequestTypeLength })
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

        if (request.NormalizedCurrency.Length > QuotationRules.MaximumCurrencyLength)
        {
            return OperationResult<PagedResult<QuotationProductPricingOptionDto>>.Fail(
                $"Currency cannot exceed {QuotationRules.MaximumCurrencyLength} characters.");
        }

        var requestType = request.NormalizedRequestType;
        var canViewSensitivePricing = ProductPricingAccessRules.CanManage(_currentUser);

        var eligibleRequests = _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == companyId &&
                x.Product.IsActive &&
                x.Product.CompanyId == companyId);

        if (request.Status is { } sampleRequestStatus)
        {
            eligibleRequests = eligibleRequests.Where(x =>
                x.Status == sampleRequestStatus.ToString());
        }

        if (requestType is not null)
        {
            eligibleRequests = eligibleRequests.Where(x => x.RequestType == requestType);
        }

        var productQuery = _dbContext.Products
            .AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == companyId);

        if (request.Status.HasValue || requestType is not null)
        {
            productQuery = productQuery.Where(product =>
                eligibleRequests.Any(sampleRequest =>
                    sampleRequest.ProductId == product.ProductId));
        }

        if (request.QuotationStatus is { } quotationStatus)
        {
            productQuery = productQuery.Where(product =>
                _dbContext.QuotationLines.Any(line =>
                    line.ProductId == product.ProductId &&
                    line.Quotation.IsActive &&
                    line.Quotation.CompanyId == companyId &&
                    line.Quotation.Status == quotationStatus));
        }

        if (request.QuotationId is { } quotationId)
        {
            productQuery = productQuery.Where(product =>
                _dbContext.QuotationLines.Any(line =>
                    line.QuotationId == quotationId &&
                    line.ProductId == product.ProductId &&
                    line.Quotation.IsActive &&
                    line.Quotation.CompanyId == companyId));
        }

        if (request.NormalizedKeyword is { } keyword)
        {
            if (IsQuotationExternalIdKeyword(keyword))
            {
                productQuery = productQuery.Where(product =>
                    _dbContext.QuotationLines.Any(line =>
                        line.ProductId == product.ProductId &&
                        line.Quotation.IsActive &&
                        line.Quotation.CompanyId == companyId &&
                        line.Quotation.ExternalId.StartsWith(keyword)));
            }
            else if (IsSampleRequestExternalIdKeyword(keyword))
            {
                productQuery = productQuery.Where(product =>
                    eligibleRequests.Any(sampleRequest =>
                        sampleRequest.ProductId == product.ProductId &&
                        sampleRequest.ExternalId.StartsWith(keyword)));
            }
            else if (IsFormulaExternalIdKeyword(keyword))
            {
                productQuery = productQuery.Where(product =>
                    _dbContext.Formulas.Any(formula =>
                        formula.IsActive &&
                        formula.CompanyId == companyId &&
                        formula.ProductId == product.ProductId &&
                        EF.Functions.ILike(formula.ExternalId, $"{keyword}%")));
            }
            else
            {
                productQuery = productQuery.Where(product =>
                    (product.ColourCode ?? string.Empty).Contains(keyword) ||
                    (product.Name ?? string.Empty).Contains(keyword) ||
                    eligibleRequests.Any(sampleRequest =>
                        sampleRequest.ProductId == product.ProductId &&
                        sampleRequest.ExternalId.Contains(keyword)) ||
                    _dbContext.Formulas.Any(formula =>
                        formula.IsActive &&
                        formula.CompanyId == companyId &&
                        formula.ProductId == product.ProductId &&
                        (EF.Functions.ILike(formula.ExternalId, $"%{keyword}%") || formula.Name.Contains(keyword))));
            }
        }

        var totalCount = await productQuery.CountAsync(cancellationToken);
        productQuery = ApplySorting(productQuery, eligibleRequests, request);

        var productCandidates = await productQuery
            .Select(x => new PricingProductCandidate
            {
                ProductId = x.ProductId,
                ProductCode = x.ColourCode ?? x.Code ?? string.Empty,
                ProductName = x.Name ?? string.Empty
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

        var latestSampleRequestRows = await eligibleRequests
            .Where(x => productIds.Contains(x.ProductId))
            .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .ThenByDescending(x => x.SampleRequestId)
            .Select(x => new PricingSampleRequestCandidate
            {
                ProductId = x.ProductId,
                SampleRequestId = x.SampleRequestId,
                SampleRequestExternalId = x.ExternalId,
                CompletedDate = x.UpdatedDate ?? x.CreatedDate,
                CustomerId = x.Customer.CustomerId,
                CustomerExternalId = x.Customer.ExternalId ?? string.Empty,
                CustomerName = x.Customer.CustomerName ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        var latestSampleRequestByProduct = latestSampleRequestRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (var product in productCandidates)
        {
            if (!latestSampleRequestByProduct.TryGetValue(product.ProductId, out var sampleRequest))
            {
                continue;
            }

            product.SampleRequestId = sampleRequest.SampleRequestId;
            product.SampleRequestExternalId = sampleRequest.SampleRequestExternalId;
            product.CompletedDate = sampleRequest.CompletedDate;
            product.CustomerId = sampleRequest.CustomerId;
            product.CustomerExternalId = sampleRequest.CustomerExternalId;
            product.CustomerName = sampleRequest.CustomerName;
        }

        var pricingVersionRows = await _crmDbContext.ProductPricingVersions
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.PriceTiers)
            .Include(x => x.SourceFormula)
            .Include(x => x.SourceManufacturingFormula)
            .Where(x =>
                x.IsActive &&
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                x.Currency == request.NormalizedCurrency &&
                (x.Status == ProductPricingStatus.Draft ||
                 x.Status == ProductPricingStatus.Approved))
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

        var pricingVersionsByProduct = pricingVersionRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var pricingSourcesByProduct = await _sourceQueryService.LoadAsync(
            productIds,
            companyId,
            request.NormalizedCurrency,
            canViewSensitivePricing,
            cancellationToken);

        var formulaRows = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                ProductPricingSourceRules.EligibleFormulaStatuses.Contains(x.Status))
            .Select(x => new PricingFormula
            {
                ProductId = x.ProductId,
                FormulaId = x.FormulaId,
                FormulaExternalId = x.ExternalId,
                FormulaName = x.Name,
                Status = x.Status,
                MaterialCost = x.TotalPrice,
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
                ItemCode = x.MaterialExternalIdSnapshot ?? string.Empty,
                ItemName = x.MaterialNameSnapshot ?? string.Empty,
                Quantity = x.Quantity,
                Unit = x.Unit ?? string.Empty,
                LineNo = x.LineNo
            })
            .OrderBy(x => x.FormulaId)
            .ThenBy(x => x.LineNo)
            .ToListAsync(cancellationToken);

        var currentItemData = await FormulaItemDisplayResolver.LoadCurrentDataAsync(
            _dbContext,
            companyId,
            materialRows
                .Where(material => material.ItemId.HasValue)
                .Select(material => new FormulaItemDisplaySource(
                    material.ItemId!.Value,
                    material.ItemType,
                    material.ItemName,
                    material.ItemCode)),
            cancellationToken);

        foreach (var material in materialRows.Where(material => material.ItemId.HasValue))
        {
            var display = FormulaItemDisplayResolver.Resolve(
                new FormulaItemDisplaySource(
                    material.ItemId!.Value,
                    material.ItemType,
                    material.ItemName,
                    material.ItemCode),
                currentItemData);
            material.ItemName = display.Name ?? string.Empty;
            material.ItemCode = display.ExternalId ?? string.Empty;
        }

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
                x =>
                {
                    var sources = pricingSourcesByProduct.GetValueOrDefault(x.Key) ?? [];
                    return (IReadOnlyList<QuotationProductPricingFormulaDto>)x
                        .Select(formula => MapFormula(
                            formula,
                            sources.FirstOrDefault(source =>
                                source.SourceType == ProductPricingSourceType.Formula &&
                                source.SourceId == formula.FormulaId),
                            materialsByFormula.GetValueOrDefault(formula.FormulaId) ?? [],
                            canViewSensitivePricing))
                        .ToList();
                });

        var items = productCandidates
            .Select(product =>
            {
                var formulas = formulasByProduct.GetValueOrDefault(product.ProductId) ?? [];
                var sources = pricingSourcesByProduct.GetValueOrDefault(product.ProductId) ?? [];
                var versions = pricingVersionsByProduct.GetValueOrDefault(product.ProductId) ?? [];
                var currentPricing = canViewSensitivePricing
                    ? versions.OrderByDescending(x => x.Version).FirstOrDefault()
                    : versions
                        .Where(x => x.Status == ProductPricingStatus.Approved)
                        .OrderByDescending(x => x.Version)
                        .FirstOrDefault();

                return new QuotationProductPricingOptionDto
                {
                    SampleRequestId = product.SampleRequestId,
                    SampleRequestExternalId = product.SampleRequestExternalId,
                    CompletedDate = product.CompletedDate,
                    HasSampleRequest = product.SampleRequestId.HasValue,
                    ProductId = product.ProductId,
                    ProductCode = product.ProductCode,
                    ProductName = product.ProductName,
                    Currency = request.NormalizedCurrency,
                    PricingStatus = ResolvePricingStatus(
                        versions.Length > 0,
                        currentPricing?.Status,
                        sources.Count > 0,
                        sources.Any(source =>
                            source.PricingStatus == FormulaPricingPolicyRules.PricingPolicyMissing)),
                    HasPricingVersion = versions.Length > 0,
                    CurrentPricing = currentPricing is null
                        ? null
                        : ProductPricingVersionMapper.ToDto(
                            currentPricing,
                            canViewSensitivePricing),

                    CustomerId = product.CustomerId,
                    CustomerExternalId = product.CustomerExternalId,
                    CustomerName = product.CustomerName,
                    HasFormula = versions.Any(x =>
                        x.SourceFormulaId.HasValue ||
                        x.SourceManufacturingFormulaId.HasValue ||
                        x.SourceManufacturingVUFormulaId.HasValue) ||
                        sources.Count > 0,
                    HasEligiblePricingSource = sources.Count > 0,
                    PricingSources = sources,
                    Formulas = formulas
                };
            })
            .ToList();

        return OperationResult<PagedResult<QuotationProductPricingOptionDto>>.Ok(
            new PagedResult<QuotationProductPricingOptionDto>(
                items,
                totalCount,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));
    }

    private static ProductPricingLookupStatus ResolvePricingStatus(
        bool hasPricingVersion,
        ProductPricingStatus? visibleStatus,
        bool hasEligibleSource,
        bool pricingPolicyMissing)
    {
        if (pricingPolicyMissing)
        {
            return ProductPricingLookupStatus.PricingPolicyMissing;
        }

        if (visibleStatus == ProductPricingStatus.Approved)
        {
            return ProductPricingLookupStatus.Approved;
        }

        if (visibleStatus == ProductPricingStatus.Draft)
        {
            return ProductPricingLookupStatus.Draft;
        }

        if (hasPricingVersion)
        {
            return ProductPricingLookupStatus.WaitingForApproval;
        }

        return hasEligibleSource
            ? ProductPricingLookupStatus.WaitingForPricing
            : ProductPricingLookupStatus.NoEligibleSource;
    }

    private static IQueryable<Product> ApplySorting(
        IQueryable<Product> query,
        IQueryable<SampleRequest> eligibleRequests,
        GetQuotationProductPricingOptionsQuery request)
    {
        return request.NormalizedSortBy?.ToLowerInvariant() switch
        {
            QuotationProductPricingSortFields.ExternalId => request.SortDescending
                ? query
                    .OrderByDescending(product => eligibleRequests
                        .Where(x => x.ProductId == product.ProductId)
                        .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                        .ThenByDescending(x => x.SampleRequestId)
                        .Select(x => x.ExternalId)
                        .FirstOrDefault())
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.ProductId)
                : query
                    .OrderBy(product => eligibleRequests
                        .Where(x => x.ProductId == product.ProductId)
                        .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                        .ThenByDescending(x => x.SampleRequestId)
                        .Select(x => x.ExternalId)
                        .FirstOrDefault())
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.ProductId),

            QuotationProductPricingSortFields.ColourCode => request.SortDescending
                ? query
                    .OrderByDescending(x => x.ColourCode)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.ProductId)
                : query
                    .OrderBy(x => x.ColourCode)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.ProductId),

            QuotationProductPricingSortFields.ProductName => request.SortDescending
                ? query
                    .OrderByDescending(x => x.Name)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.ProductId)
                : query
                    .OrderBy(x => x.Name)
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.ProductId),

            QuotationProductPricingSortFields.UpdatedDate => request.SortDescending
                ? query
                    .OrderByDescending(product => eligibleRequests
                        .Where(x => x.ProductId == product.ProductId)
                        .Max(x => (DateTime?)(x.UpdatedDate ?? x.CreatedDate)))
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.ProductId)
                : query
                    .OrderBy(product => eligibleRequests
                        .Where(x => x.ProductId == product.ProductId)
                        .Max(x => (DateTime?)(x.UpdatedDate ?? x.CreatedDate)))
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.ProductId),

            QuotationProductPricingSortFields.CreatedDate => request.SortDescending
                ? query
                    .OrderByDescending(product => eligibleRequests
                        .Where(x => x.ProductId == product.ProductId)
                        .Max(x => (DateTime?)x.CreatedDate))
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.ProductId)
                : query
                    .OrderBy(product => eligibleRequests
                        .Where(x => x.ProductId == product.ProductId)
                        .Max(x => (DateTime?)x.CreatedDate))
                    .ThenByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.ProductId),

            _ => query
                .OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.ProductId)
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
            LatestTotalPrice = hasPrice
                ? PricingRoundingRules.RoundCalculatedPrice(
                    material.Quantity * latestPrice!.CurrentPrice)
                : null,
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
        ProductPricingSourceOptionDto? source,
        IReadOnlyList<QuotationProductPricingMaterialDto> materials,
        bool canViewSensitivePricing)
    {
        if (!canViewSensitivePricing)
        {
            return new QuotationProductPricingFormulaDto
            {
                PricingStatus = source?.PricingStatus ?? FormulaPricingPolicyRules.PricingPolicyMissing,
                FormulaId = formula.FormulaId,
                FormulaExternalId = formula.FormulaExternalId,
                FormulaName = formula.FormulaName,
                Status = formula.Status,
                IsCustomerSelected = formula.IsSelected,
                StandardSellingPrice = source?.StandardSellingPrice,
                SuggestedPriceTiers = source?.PriceTierTemplates ?? []
            };
        }

        return new QuotationProductPricingFormulaDto
        {
            PricingStatus = source?.PricingStatus ?? FormulaPricingPolicyRules.PricingPolicyMissing,
            FormulaPricingPolicyId = source?.FormulaPricingPolicyId,
            FormulaPricingPolicyVersion = source?.FormulaPricingPolicyVersion,
            FormulaId = formula.FormulaId,
            FormulaExternalId = formula.FormulaExternalId,
            FormulaName = formula.FormulaName,
            Status = formula.Status,
            IsCustomerSelected = formula.IsSelected,
            MaterialCost = formula.MaterialCost,
            RealtimeMaterialCost = source?.CurrentMaterialCost,
            IsRealtimeMaterialCostComplete = source?.IsCurrentMaterialCostComplete,
            MissingMaterialPriceCount = source?.MissingMaterialPriceCount,
            ManufacturingCost = source?.ManufacturingCost,
            StandardSellingPrice = source?.StandardSellingPrice,
            ProfitMarginRate = source?.ProfitMarginRate,
            PricingUpdatedDate = formula.PricingUpdatedDate,
            Pricing = source?.Pricing,
            SuggestedPriceTiers = source?.PriceTierTemplates ?? [],
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
