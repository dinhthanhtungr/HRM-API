using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Tạo snapshot dòng báo giá từ sản phẩm thật sau khi kiểm tra sản phẩm active và cùng công ty.
/// </summary>
internal sealed class QuotationLineBuilder
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly CustomerSampleSharingService? _sampleSharing;

    public QuotationLineBuilder(
        ICRMReadDbContext dbContext, CustomerSampleSharingService? sampleSharing = null)
    {
        _dbContext = dbContext;
        _sampleSharing = sampleSharing;
    }

    public async Task<OperationResult<IReadOnlyList<QuotationLine>>> BuildAsync(
        Guid quotationId,
        Guid companyId,
        Guid customerId,
        string currency,
        decimal exchangeRate,
        IReadOnlyList<QuotationLineRequest> requests,
        CancellationToken cancellationToken,
        IReadOnlySet<Guid>? retainedStandardReferences = null)
    {
        if (requests.Count > QuotationRules.MaximumLineCount)
        {
            return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                $"A quotation cannot contain more than {QuotationRules.MaximumLineCount} lines.");
        }

        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            if (request.ProductId == Guid.Empty || request.Quantity <= 0m || request.UnitPrice < 0m ||
                request.SampleRequestId == Guid.Empty || request.ProductPricingVersionId == Guid.Empty ||
                !Enum.IsDefined(request.PriceMode))
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                    $"Quotation line at index {index} is invalid.");
            }

            if (!QuotationRules.IsValidPercent(request.DiscountPercent))
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                    $"DiscountPercent at index {index} must be between 0 and 100.");
            }

            if (request.SortOrder is < 0)
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                    $"SortOrder at index {index} cannot be negative.");
            }
        }

        var productIds = requests.Select(x => x.ProductId).Distinct().ToArray();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(x =>
                productIds.Contains(x.ProductId) &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new
            {
                x.ProductId,
                x.ColourCode,
                x.Code,
                x.Additive,
                x.CategoryId,
                x.Name,
                x.Unit
            })
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);

        if (products.Count != productIds.Length)
        {
            return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                "One or more products were not found, inactive, or outside the current company.");
        }

        var manualRequests = requests
            .Where(x => x.PriceMode == QuotationLinePriceMode.ManualAuthorized)
            .ToArray();
        if (manualRequests.Length > 0)
        {
            if (manualRequests.Any(x => x.ProductPricingVersionId.HasValue &&
                retainedStandardReferences?.Contains(x.ProductPricingVersionId.Value) != true))
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                    "Manual authorized pricing cannot reference a standard pricing version.");
            }
        }

        var pricingVersionIds = requests
            .Where(x => x.ProductPricingVersionId.HasValue)
            .Select(x => x.ProductPricingVersionId!.Value)
            .Distinct()
            .ToArray();
        var pricingVersions = await _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Include(x => x.PriceTiers)
            .Where(x =>
                pricingVersionIds.Contains(x.ProductPricingVersionId) &&
                x.CompanyId == companyId &&
                x.Currency == ProductPricingSourceRules.StandardPricingCurrency &&
                ((x.Status == HRM.Domain.Enums.CustomerEnum.ProductPricingStatus.Approved && x.IsActive) ||
                 (retainedStandardReferences != null && retainedStandardReferences.Contains(x.ProductPricingVersionId))))
            .ToDictionaryAsync(x => x.ProductPricingVersionId, cancellationToken);

        if (pricingVersions.Count != pricingVersionIds.Length || requests.Any(request =>
                request.ProductPricingVersionId.HasValue &&
                (!pricingVersions.TryGetValue(request.ProductPricingVersionId.Value, out var pricingVersion) ||
                 pricingVersion.ProductId != request.ProductId ||
                 pricingVersion.StandardSellingPrice is null or < 0m)))
        {
            return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                "One or more product pricing versions were not found, not approved, outside the current company/VND standard pricing, or belong to another product.");
        }

        if (pricingVersionIds.Length > 0 &&
            !QuotationPricingCurrencyConverter.TryValidateQuotationCurrency(
                currency,
                exchangeRate,
                out var currencyError))
        {
            return OperationResult<IReadOnlyList<QuotationLine>>.Fail(currencyError!);
        }

        var sampleRequestIds = requests
            .Where(x => x.SampleRequestId.HasValue)
            .Select(x => x.SampleRequestId!.Value)
            .Distinct()
            .ToArray();
        Guid[] sharedOwners = [];
        if (sampleRequestIds.Length > 0 && _sampleSharing?.Enabled == true)
        {
            var owners = await _sampleSharing.ResolveOwnerIdsAsync(companyId, customerId, cancellationToken);
            if (owners is null)
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail("Không được dùng mẫu cho khách nhận báo giá này.");
            sharedOwners = owners;
        }
        var sampleRequests = await ScopeSampleRequests(
                _dbContext.SampleRequests.AsNoTracking(), companyId, customerId, sharedOwners)
            .Where(x => sampleRequestIds.Contains(x.SampleRequestId))
            .Select(x => new
            {
                x.SampleRequestId,
                x.ProductId
            })
            .ToDictionaryAsync(x => x.SampleRequestId, cancellationToken);

        if (sampleRequests.Count != sampleRequestIds.Length || requests.Any(request =>
                request.SampleRequestId.HasValue &&
                (!sampleRequests.TryGetValue(request.SampleRequestId.Value, out var sampleRequest) ||
                 sampleRequest.ProductId != request.ProductId)))
        {
            return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                "One or more sample requests were not found, inactive, outside the current company/customer, or belong to another product.");
        }

        var lines = new List<QuotationLine>(requests.Count);
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var product = products[request.ProductId];
            var productCode = QuotationRules.TrimToNull(product.ColourCode);
            var productName = QuotationRules.TrimToNull(product.Name);
            var unit = QuotationRules.TrimToNull(request.Unit) ?? QuotationRules.TrimToNull(product.Unit);
            var quotationLineId = Guid.CreateVersion7();
            var standardSellingPrice = request.ProductPricingVersionId.HasValue &&
                retainedStandardReferences?.Contains(request.ProductPricingVersionId.Value) != true
                ? QuotationPricingCurrencyConverter.ConvertFromStandardPricing(
                    pricingVersions[request.ProductPricingVersionId.Value].StandardSellingPrice!.Value,
                    currency,
                    exchangeRate)
                : request.UnitPrice;
            var pricingResult = QuotationPriceTierBuilder.Build(
                quotationLineId,
                request.Quantity,
                request.PriceTiers,
                $"lines[{index}]");
            if (!pricingResult.Success || pricingResult.Data is null)
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(pricingResult.Message!);
            }

            if (productCode is null || productName is null || unit is null)
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                    $"Product {request.ProductId} must have code, name, and unit before it can be quoted.");
            }

            if (unit.Length > QuotationRules.MaximumUnitLength)
            {
                return OperationResult<IReadOnlyList<QuotationLine>>.Fail(
                    $"Unit at index {index} cannot exceed {QuotationRules.MaximumUnitLength} characters.");
            }

            lines.Add(new QuotationLine
            {
                QuotationLineId = quotationLineId,
                QuotationId = quotationId,
                ProductId = request.ProductId,
                SampleRequestId = request.SampleRequestId,
                ProductPricingVersionId = request.ProductPricingVersionId,
                ProductExternalIdSnapshot = productCode,
                ProductNameSnapshot = productName,
                Quantity = request.Quantity,
                Unit = unit,
                PriceMode = request.PriceMode,
                UnitPrice = standardSellingPrice,
                DiscountPercent = request.DiscountPercent,
                LineTotal = QuotationRules.CalculateLineTotal(
                    request.Quantity,
                    standardSellingPrice,
                    request.DiscountPercent),
                PriceTiers = pricingResult.Data.PriceTiers.ToList(),
                Note = QuotationRules.TrimToNull(request.Note),
                SortOrder = request.SortOrder ?? index,
                IsActive = request.IsActive
            });
        }

        return OperationResult<IReadOnlyList<QuotationLine>>.Ok(lines);
    }

    internal static IQueryable<SampleRequest> ScopeSampleRequests(
        IQueryable<SampleRequest> source, Guid companyId, Guid customerId, IReadOnlyCollection<Guid>? sharedOwners = null)
    {
        sharedOwners ??= [];
        // Membership only authorizes the sample reference, never another customer's prices.
        return source.Where(x =>
            x.CompanyId == companyId &&
            x.IsActive &&
            (x.CustomerId == customerId ||
             (sharedOwners.Contains(x.CustomerId) && x.Customer.CompanyId == companyId && x.Customer.IsActive == true &&
              (x.RequestType == null || x.RequestType != HRM.Application.Features.PLM.Shared.Rules.SemiFinishedRequestRules.RequestType)) ||
             (x.Customer.CompanyId == companyId &&
              x.Customer.ExternalId == InternalCustomerRules.InternalCustomerExternalId)));
    }
}
