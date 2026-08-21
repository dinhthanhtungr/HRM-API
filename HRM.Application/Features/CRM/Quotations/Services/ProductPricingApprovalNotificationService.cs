using System.Text.Json;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class ProductPricingApprovalNotificationService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly ICRMWriteDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public ProductPricingApprovalNotificationService(
        ICRMWriteDbContext dbContext,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task PublishAsync(
        ProductPricingVersion pricingVersion,
        Guid companyId,
        Guid approvedByEmployeeId,
        CancellationToken cancellationToken)
    {
        var targets = await _dbContext.Quotations
            .AsNoTracking()
            .Where(quotation =>
                quotation.CompanyId == companyId &&
                quotation.IsActive &&
                quotation.Status == QuotationStatus.Draft &&
                quotation.Currency == pricingVersion.Currency &&
                quotation.SaleEmployeeId != approvedByEmployeeId &&
                quotation.SaleEmployee.IsActive &&
                quotation.SaleEmployee.CompanyId == companyId &&
                quotation.Lines.Any(line => line.ProductId == pricingVersion.ProductId))
            .Select(quotation => new WaitingQuotationTarget(
                quotation.QuotationId,
                quotation.ExternalId,
                quotation.SaleEmployeeId))
            .Distinct()
            .ToListAsync(cancellationToken);
        if (targets.Count == 0)
        {
            return;
        }

        var actorName = await _dbContext.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.EmployeeId == approvedByEmployeeId &&
                employee.CompanyId == companyId &&
                employee.IsActive)
            .Select(employee => employee.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        actorName = QuotationRules.TrimToNull(actorName) ?? "Ban giám đốc";

        var productCode = QuotationRules.TrimToNull(pricingVersion.Product.ColourCode) ??
            QuotationRules.TrimToNull(pricingVersion.Product.Code) ??
            string.Empty;
        var productName = QuotationRules.TrimToNull(pricingVersion.Product.Name) ??
            string.Empty;

        foreach (var target in targets)
        {
            var payload = new QuotationPricingApprovedNotificationPayload
            {
                QuotationId = target.QuotationId,
                QuotationExternalId = target.QuotationExternalId,
                ProductId = pricingVersion.ProductId,
                ProductCode = productCode,
                ProductPricingVersionId = pricingVersion.ProductPricingVersionId,
                ProductPricingVersion = pricingVersion.Version,
                Action = new QuotationPricingApprovedActionDto
                {
                    Parameters = new QuotationPricingApprovedActionParametersDto
                    {
                        QuotationId = target.QuotationId,
                        QuotationExternalId = target.QuotationExternalId
                    }
                }
            };

            await _notificationService.PublishAsync(
                new PublishNotificationRequest
                {
                    CompanyId = companyId,
                    CreatedBy = approvedByEmployeeId,
                    CreatedByNameSnapshot = actorName,
                    Topic = TopicNotifications.QuotationPricingApproved,
                    Severity = NotificationSeverity.Info,
                    Title = $"Giá đã duyệt cho {target.QuotationExternalId}",
                    Message = $"{actorName} đã duyệt giá sản phẩm [{productCode}] {productName}.",
                    Link = null,
                    AggregateId = target.QuotationId,
                    AggregateCode = target.QuotationExternalId,
                    PayloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions),
                    TargetUserIds = [target.SaleEmployeeId]
                },
                cancellationToken);
        }
    }

    private sealed record WaitingQuotationTarget(
        Guid QuotationId,
        string QuotationExternalId,
        Guid SaleEmployeeId);
}
