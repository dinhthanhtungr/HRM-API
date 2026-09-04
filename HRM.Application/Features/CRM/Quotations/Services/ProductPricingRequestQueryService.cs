using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class ProductPricingRequestQueryService
{
    private const string QuotationRequestedPayload =
        """{"contentType":"QuotationRequested"}""";

    private readonly ICRMReadDbContext _crmDbContext;
    private readonly IInternalMailDbContext _internalMailDbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public ProductPricingRequestQueryService(
        ICRMReadDbContext crmDbContext,
        IInternalMailDbContext internalMailDbContext,
        ICustomerVisibilityService visibilityService)
    {
        _crmDbContext = crmDbContext;
        _internalMailDbContext = internalMailDbContext;
        _visibilityService = visibilityService;
    }

    public async Task<IReadOnlyList<ProductPricingRequestRow>> LoadAsync(
        Guid companyId,
        string currency,
        IReadOnlyCollection<Guid>? productIds,
        CancellationToken cancellationToken)
    {
        var requestRows = await _internalMailDbContext.InternalConversations
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.Quotation &&
                x.RelatedId.HasValue)
            .Select(x => new
            {
                QuotationId = x.RelatedId!.Value,
                RequestedAt = x.Messages
                    .Where(message =>
                        !message.IsDeleted &&
                        message.PayloadJson != null &&
                        EF.Functions.JsonContains(
                            message.PayloadJson,
                            QuotationRequestedPayload))
                    .Max(message => (DateTime?)message.SentAt)
            })
            .Where(x => x.RequestedAt.HasValue)
            .ToListAsync(cancellationToken);
        if (requestRows.Count == 0)
        {
            return [];
        }

        var requestedAtByQuotation = requestRows
            .GroupBy(x => x.QuotationId)
            .ToDictionary(x => x.Key, x => x.Max(y => y.RequestedAt)!.Value);
        var quotationIds = requestedAtByQuotation.Keys.ToArray();
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var query = _visibilityService
            .ApplyQuotationVisibility(
                _crmDbContext.Quotations.AsNoTracking(),
                _crmDbContext.Customers.AsNoTracking(),
                scope)
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Status == QuotationStatus.PendingApproval &&
                x.Currency == currency &&
                quotationIds.Contains(x.QuotationId))
            .SelectMany(x => x.Lines.Where(line => line.IsActive).Select(line => new ProductPricingRequestRow
            {
                ProductId = line.ProductId,
                QuotationId = x.QuotationId,
                QuotationExternalId = x.ExternalId,
                CustomerId = x.CustomerId,
                CustomerExternalId = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName,
                SaleEmployeeId = x.SaleEmployeeId,
                SaleEmployeeName = x.SaleEmployee.FullName,
                Quantity = line.Quantity,
                Unit = line.Unit
            }));

        if (productIds is { Count: > 0 })
        {
            query = query.Where(x => productIds.Contains(x.ProductId));
        }

        var rows = await query.ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.RequestedAt = requestedAtByQuotation[row.QuotationId];
        }

        return rows;
    }

    /// <summary>
    /// Lấy toàn bộ khách hàng liên quan đến sản phẩm qua Sample Request hoặc quotation active.
    /// Visibility của từng nguồn vẫn được áp dụng trước khi trả dữ liệu.
    /// </summary>
    public async Task<IReadOnlyList<ProductPricingRelatedCustomerRow>> LoadRelatedCustomersAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotationRows = await _visibilityService
            .ApplyQuotationVisibility(
                _crmDbContext.Quotations.AsNoTracking(),
                _crmDbContext.Customers.AsNoTracking(),
                scope)
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Customer.ExternalId != InternalCustomerRules.InternalCustomerExternalId &&
                x.Lines.Any(line => line.IsActive && productIds.Contains(line.ProductId)))
            .SelectMany(x => x.Lines
                .Where(line => line.IsActive && productIds.Contains(line.ProductId))
                .Select(line => new ProductPricingRelatedCustomerRow
                {
                    ProductId = line.ProductId,
                    RelatedDocumentId = x.QuotationId,
                    CustomerId = x.CustomerId,
                    CustomerExternalId = x.Customer.ExternalId,
                    CustomerName = x.Customer.CustomerName,
                    RelatedDate = x.UpdatedDate ?? x.CreatedDate
                }))
            .ToListAsync(cancellationToken);

        var sampleRequestRows = await _visibilityService
            .ApplySampleRequestVisibility(
                _crmDbContext.SampleRequests.AsNoTracking(),
                _crmDbContext.Customers.AsNoTracking(),
                scope)
            .Where(x =>
                x.CompanyId == companyId &&
                x.Customer.ExternalId != InternalCustomerRules.InternalCustomerExternalId &&
                productIds.Contains(x.ProductId))
            .Select(x => new ProductPricingRelatedCustomerRow
            {
                ProductId = x.ProductId,
                RelatedDocumentId = x.SampleRequestId,
                CustomerId = x.CustomerId,
                CustomerExternalId = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName,
                RelatedDate = x.UpdatedDate ?? x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return [.. quotationRows, .. sampleRequestRows];
    }
}

internal sealed class ProductPricingRequestRow
{
    public Guid ProductId { get; init; }
    public Guid QuotationId { get; init; }
    public string QuotationExternalId { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid SaleEmployeeId { get; init; }
    public string SaleEmployeeName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateTime RequestedAt { get; set; }
}

internal sealed class ProductPricingRelatedCustomerRow
{
    public Guid ProductId { get; init; }
    public Guid RelatedDocumentId { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public DateTime RelatedDate { get; init; }
}
