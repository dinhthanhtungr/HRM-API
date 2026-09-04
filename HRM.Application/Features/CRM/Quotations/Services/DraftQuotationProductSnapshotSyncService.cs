using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Đồng bộ mã sản phẩm mới của Lab vào các báo giá nháp đang tham chiếu sản phẩm đó.
/// </summary>
internal sealed class DraftQuotationProductSnapshotSyncService
{
    private readonly ICRMWriteDbContext _dbContext;
    private readonly QuotationConversationSubjectService _conversationSubjectService;

    public DraftQuotationProductSnapshotSyncService(
        ICRMWriteDbContext dbContext,
        QuotationConversationSubjectService conversationSubjectService)
    {
        _dbContext = dbContext;
        _conversationSubjectService = conversationSubjectService;
    }

    public async Task SyncColourCodeAsync(
        Guid productId,
        Guid companyId,
        string? colourCode,
        Guid? updatedBy,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        var normalizedColourCode = QuotationRules.TrimToNull(colourCode);
        if (productId == Guid.Empty ||
            companyId == Guid.Empty ||
            normalizedColourCode is null)
        {
            return;
        }

        var quotations = await _dbContext.Quotations
            .AsTracking()
            .Include(x => x.Lines)
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Status == QuotationStatus.Draft &&
                x.Lines.Any(line => line.IsActive && line.ProductId == productId))
            .OrderBy(x => x.QuotationId)
            .ToListAsync(cancellationToken);

        foreach (var quotation in quotations)
        {
            var snapshotChanged = false;
            foreach (var line in quotation.Lines.Where(x => x.IsActive && x.ProductId == productId))
            {
                if (string.Equals(
                    line.ProductExternalIdSnapshot,
                    normalizedColourCode,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                line.ProductExternalIdSnapshot = normalizedColourCode;
                snapshotChanged = true;
            }

            if (snapshotChanged)
            {
                quotation.UpdatedBy = updatedBy;
                quotation.UpdatedDate = updatedAt;
            }

            await _conversationSubjectService.SyncSubjectAsync(
                quotation.QuotationId,
                quotation.CompanyId,
                quotation.ExternalId,
                quotation.Lines.Where(x => x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.QuotationLineId)
                    .Select(x => x.ProductExternalIdSnapshot),
                cancellationToken);
        }
    }
}
