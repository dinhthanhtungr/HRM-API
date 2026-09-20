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
    private const string QuotationRequestWithdrawnPayload =
        """{"contentType":"QuotationPricingRequestWithdrawn"}""";

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
        IReadOnlyCollection<Guid>? productIds,
        CancellationToken cancellationToken)
    {
        var requestedQuotations = _internalMailDbContext.InternalMessages
            .AsNoTracking()
            .Where(message =>
                !message.IsDeleted &&
                message.PayloadJson != null &&
                EF.Functions.JsonContains(
                    message.PayloadJson,
                    QuotationRequestedPayload) &&
                message.Conversation.CompanyId == companyId &&
                message.Conversation.IsActive &&
                message.Conversation.RelatedType == InternalMailRelatedType.Quotation &&
                message.Conversation.RelatedId.HasValue)
            .GroupBy(message => message.Conversation.RelatedId!.Value)
            .Select(group => new
            {
                QuotationId = group.Key,
                RequestedAt = group.Max(message => message.SentAt)
            });
        var activeRequestedQuotations = requestedQuotations.Where(requested =>
            !_internalMailDbContext.InternalMessages
                .AsNoTracking()
                .Any(message =>
                    !message.IsDeleted &&
                    message.PayloadJson != null &&
                    EF.Functions.JsonContains(
                        message.PayloadJson,
                        QuotationRequestWithdrawnPayload) &&
                    message.Conversation.CompanyId == companyId &&
                    message.Conversation.IsActive &&
                    message.Conversation.RelatedType == InternalMailRelatedType.Quotation &&
                    message.Conversation.RelatedId == requested.QuotationId &&
                    message.SentAt >= requested.RequestedAt));
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleQuotations = _visibilityService
            .ApplyQuotationVisibility(
                _crmDbContext.Quotations.AsNoTracking(),
                _crmDbContext.Customers.AsNoTracking(),
                scope)
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive);

        var query =
            from quotation in visibleQuotations
            join requested in activeRequestedQuotations
                on quotation.QuotationId equals requested.QuotationId
            from line in quotation.Lines.Where(line => line.IsActive)
            select new ProductPricingRequestRow
            {
                ProductId = line.ProductId,
                QuotationId = quotation.QuotationId,
                QuotationExternalId = quotation.ExternalId,
                CustomerId = quotation.CustomerId,
                CustomerExternalId = quotation.Customer.ExternalId,
                CustomerName = quotation.Customer.CustomerName,
                SaleEmployeeId = quotation.SaleEmployeeId,
                SaleEmployeeName = quotation.SaleEmployee.FullName,
                Quantity = line.Quantity,
                Unit = line.Unit,
                RequestedAt = requested.RequestedAt
            };

        if (productIds is { Count: > 0 })
        {
            query = query.Where(x => productIds.Contains(x.ProductId));
        }

        return await query.ToListAsync(cancellationToken);
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
