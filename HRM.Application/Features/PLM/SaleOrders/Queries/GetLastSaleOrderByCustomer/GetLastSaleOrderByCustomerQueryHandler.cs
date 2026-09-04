using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetLastSaleOrderByCustomer;

/// <summary>
/// Tìm dòng SaleOrder active mới nhất theo CustomerId và ProductId, loại trừ dòng đã hủy
/// và không trả dữ liệu thuộc công ty khác.
/// </summary>
internal sealed class GetLastSaleOrderByCustomerQueryHandler
    : IRequestHandler<GetLastSaleOrderByCustomerQuery, SaleOrderProductDefaultsDto?>
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly ICRMReadDbContext _crmDbContext;
    private readonly ICustomerVisibilityService _customerVisibilityService;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly ProductPricingSourceQueryService _pricingSourceQueryService;
    private readonly QuotationProductTierPricingResolver _tierPricingResolver;

    public GetLastSaleOrderByCustomerQueryHandler(
        ISaleOrderDbContext dbContext,
        ICRMReadDbContext crmDbContext,
        ICustomerVisibilityService customerVisibilityService,
        IPLMFieldVisibilityService fieldVisibility,
        ProductPricingSourceQueryService pricingSourceQueryService,
        QuotationProductTierPricingResolver tierPricingResolver)
    {
        _dbContext = dbContext;
        _crmDbContext = crmDbContext;
        _customerVisibilityService = customerVisibilityService;
        _fieldVisibility = fieldVisibility;
        _pricingSourceQueryService = pricingSourceQueryService;
        _tierPricingResolver = tierPricingResolver;
    }

    public async Task<SaleOrderProductDefaultsDto?> Handle(
        GetLastSaleOrderByCustomerQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty || request.ProductId == Guid.Empty)
        {
            return null;
        }

        var scope = await _customerVisibilityService.BuildScopeAsync(cancellationToken);
        var customerIsVisible = await _customerVisibilityService.ApplyCustomerVisibility(
                _dbContext.Customers.AsNoTracking(),
                scope)
            .AnyAsync(x => x.CustomerId == request.CustomerId, cancellationToken);
        if (!customerIsVisible)
        {
            return null;
        }

        var orderQuery = _customerVisibilityService.ApplyMerchandiseOrderVisibility(
            _dbContext.MerchandiseOrders.AsNoTracking(),
            _dbContext.Customers.AsNoTracking(),
            scope);

        var previousSale = await orderQuery
            .Where(order =>
                order.CustomerId == request.CustomerId &&
                order.IsActive)
            .SelectMany(order => order.MerchandiseOrderDetails, (order, detail) => new { order, detail })
            .Where(x =>
                x.detail.ProductId == request.ProductId &&
                x.detail.IsActive &&
                x.detail.Status != MerchadiseStatus.Cancelled.ToString())
            .OrderByDescending(x => x.order.CreateDate)
            .ThenByDescending(x => x.order.MerchandiseOrderId)
            .ThenByDescending(x => x.detail.MerchandiseOrderDetailId)
            .Select(x => new SaleOrderProductDefaultsDto
            {
                HasPreviousSale = true,
                MerchandiseOrderId = x.order.MerchandiseOrderId,
                MerchandiseOrderDetailId = x.detail.MerchandiseOrderDetailId,
                ProductId = x.detail.ProductId,
                FormulaId = x.detail.FormulaId,
                BagType = x.detail.BagType,
                PackageWeight = x.detail.PackageWeight,
                ExpectedQuantity = x.detail.ExpectedQuantity,
                FormulaExternalIdSnapshot = x.detail.FormulaExternalIdSnapshot,
                Comment = x.detail.Comment,
                UnitPriceAgreed = x.detail.UnitPriceAgreed,
                CreateDate = x.order.CreateDate
            })
            .FirstOrDefaultAsync(cancellationToken);

        const string currency = "VND";
        var visibleQuotations = _customerVisibilityService.ApplyQuotationVisibility(
            _crmDbContext.Quotations.AsNoTracking(),
            _crmDbContext.Customers.AsNoTracking(),
            scope);
        var pricingReferences = await _tierPricingResolver.ResolveAsync(
            [request.ProductId],
            scope.CompanyId,
            currency,
            request.CustomerId,
            null,
            visibleQuotations,
            cancellationToken);
        var tierSelection = pricingReferences.TryGetValue(request.ProductId, out var references)
            ? SaleOrderPriceTierSelector.Select(references)
            : SaleOrderPriceTierSelection.Unavailable;

        var currentPricing = await ResolveCurrentPricingAsync(
            request.ProductId,
            scope.CompanyId,
            _fieldVisibility.CanViewFormulaPrices(),
            tierSelection,
            cancellationToken);

        return previousSale is null
            ? new SaleOrderProductDefaultsDto
            {
                HasPreviousSale = false,
                CurrentPricing = currentPricing
            }
            : new SaleOrderProductDefaultsDto
            {
                HasPreviousSale = true,
                MerchandiseOrderId = previousSale.MerchandiseOrderId,
                MerchandiseOrderDetailId = previousSale.MerchandiseOrderDetailId,
                ProductId = previousSale.ProductId,
                FormulaId = previousSale.FormulaId,
                BagType = previousSale.BagType,
                PackageWeight = previousSale.PackageWeight,
                ExpectedQuantity = previousSale.ExpectedQuantity,
                FormulaExternalIdSnapshot = previousSale.FormulaExternalIdSnapshot,
                Comment = previousSale.Comment,
                UnitPriceAgreed = previousSale.UnitPriceAgreed,
                CreateDate = previousSale.CreateDate,
                CurrentPricing = currentPricing
            };
    }

    private async Task<SaleOrderCurrentPricingDto> ResolveCurrentPricingAsync(
        Guid productId,
        Guid companyId,
        bool canViewFormulaPrices,
        SaleOrderPriceTierSelection tierSelection,
        CancellationToken cancellationToken)
    {
        const string currency = "VND";

        var approvedPricing = await _crmDbContext.ProductPricingVersions
            .AsNoTracking()
            .Include(x => x.FormulaPricingPolicy)
            .Where(x =>
                x.CompanyId == companyId &&
                x.ProductId == productId &&
                x.Currency == currency &&
                x.IsActive &&
                x.Status == ProductPricingStatus.Approved &&
                x.StandardSellingPrice.HasValue)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (approvedPricing is not null)
        {
            return new SaleOrderCurrentPricingDto
            {
                Source = "ApprovedStandardPrice",
                Currency = currency,
                SuggestedUnitPrice = approvedPricing.StandardSellingPrice,
                ProductPricingVersionId = approvedPricing.ProductPricingVersionId,
                ProductPricingVersion = approvedPricing.Version,
                ApprovedAt = approvedPricing.ApprovedAt,
                CalculatedAt = approvedPricing.CalculatedAt,
                FormulaPricingPolicyId = approvedPricing.FormulaPricingPolicyId,
                FormulaPricingPolicyVersion = approvedPricing.FormulaPricingPolicy?.Version,
                PricingPolicyEffectiveFrom = approvedPricing.FormulaPricingPolicy?.EffectiveFrom,
                PricingSourceId = approvedPricing.SourceManufacturingFormulaId ?? approvedPricing.SourceFormulaId,
                PricingSourceType = approvedPricing.SourceManufacturingFormulaId.HasValue
                    ? ProductPricingSourceType.ManufacturingFormula.ToString()
                    : approvedPricing.SourceFormulaId.HasValue
                        ? ProductPricingSourceType.Formula.ToString()
                        : string.Empty,
                PricingSourceExternalId = approvedPricing.FormulaExternalIdSnapshot ?? string.Empty,
                PricingStatus = ProductPricingStatus.Approved.ToString(),
                MaterialCost = canViewFormulaPrices ? approvedPricing.MaterialCostSnapshot : null,
                ManufacturingCost = canViewFormulaPrices ? approvedPricing.ManufacturingCost : null,
                CostBase = canViewFormulaPrices && approvedPricing.MaterialCostSnapshot.HasValue &&
                           approvedPricing.ManufacturingCost.HasValue
                    ? approvedPricing.MaterialCostSnapshot.Value + approvedPricing.ManufacturingCost.Value
                    : null,
                ProfitMarginRate = canViewFormulaPrices ? approvedPricing.ProfitMarginRate : null,
                PriceTierSource = tierSelection.Source,
                PriceTierSourceDate = tierSelection.SourceDate,
                PriceTierQuotationId = tierSelection.QuotationId,
                PriceTierQuotationExternalId = tierSelection.QuotationExternalId,
                PriceTiers = MapPriceTiers(tierSelection)
            };
        }

        var pricingSources = await _pricingSourceQueryService.LoadAsync(
            [productId],
            companyId,
            currency,
            canViewFormulaPrices,
            cancellationToken);
        var source = ProductPricingWorkbenchSourceSelector.ChooseFallback(
            pricingSources.GetValueOrDefault(productId) ?? []);
        if (source is null)
        {
            return new SaleOrderCurrentPricingDto
            {
                Currency = currency,
                PriceTierSource = tierSelection.Source,
                PriceTierSourceDate = tierSelection.SourceDate,
                PriceTierQuotationId = tierSelection.QuotationId,
                PriceTierQuotationExternalId = tierSelection.QuotationExternalId,
                PriceTiers = MapPriceTiers(tierSelection)
            };
        }

        var policyEffectiveFrom = source.FormulaPricingPolicyId.HasValue
            ? await _crmDbContext.FormulaPricingPolicies
                .AsNoTracking()
                .Where(x =>
                    x.FormulaPricingPolicyId == source.FormulaPricingPolicyId.Value &&
                    x.CompanyId == companyId)
                .Select(x => x.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new SaleOrderCurrentPricingDto
        {
            Source = "SystemCalculated",
            Currency = currency,
            SuggestedUnitPrice = source.StandardSellingPrice,
            FormulaPricingPolicyId = source.FormulaPricingPolicyId,
            FormulaPricingPolicyVersion = source.FormulaPricingPolicyVersion,
            PricingPolicyEffectiveFrom = policyEffectiveFrom,
            PricingSourceId = source.SourceId,
            PricingSourceType = source.SourceType.ToString(),
            PricingSourceExternalId = source.ExternalId,
            PricingStatus = source.PricingStatus,
            MaterialCost = canViewFormulaPrices ? source.CurrentMaterialCost : null,
            ManufacturingCost = canViewFormulaPrices ? source.ManufacturingCost : null,
            CostBase = canViewFormulaPrices ? source.Pricing?.CostBase : null,
            ProfitMarginRate = canViewFormulaPrices ? source.ProfitMarginRate : null,
            IsMaterialCostComplete = canViewFormulaPrices ? source.IsCurrentMaterialCostComplete : null,
            MissingMaterialPriceCount = canViewFormulaPrices ? source.MissingMaterialPriceCount : null,
            PriceTierSource = tierSelection.Source,
            PriceTierSourceDate = tierSelection.SourceDate,
            PriceTierQuotationId = tierSelection.QuotationId,
            PriceTierQuotationExternalId = tierSelection.QuotationExternalId,
            PriceTiers = MapPriceTiers(tierSelection)
        };
    }

    private static IReadOnlyList<SaleOrderSuggestedPriceTierDto> MapPriceTiers(
        SaleOrderPriceTierSelection selection)
    {
        return selection.PriceTiers
            .OrderBy(x => x.SortOrder)
            .Select(x => new SaleOrderSuggestedPriceTierDto
            {
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                SortOrder = x.SortOrder
            })
            .ToArray();
    }
}
