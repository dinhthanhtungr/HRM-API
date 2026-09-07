using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Đồng bộ tiêu đề cuộc trao đổi báo giá với các mã sản phẩm đang có trên báo giá.
/// </summary>
internal sealed class QuotationConversationSubjectService
{
    private readonly IInternalMailDbContext _dbContext;

    public QuotationConversationSubjectService(IInternalMailDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static string BuildSubject(
        string quotationExternalId,
        IEnumerable<string?> productCodes)
    {
        var baseSubject = $"Báo giá {quotationExternalId.Trim()}";
        var normalizedProductCodes = productCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedProductCodes.Length == 0)
        {
            return baseSubject;
        }

        return $"{baseSubject} - {string.Join(", ", normalizedProductCodes)}";
    }

    public async Task SyncSubjectAsync(
        Guid quotationId,
        Guid companyId,
        string quotationExternalId,
        IEnumerable<string?> productCodes,
        CancellationToken cancellationToken)
    {
        if (quotationId == Guid.Empty ||
            companyId == Guid.Empty ||
            string.IsNullOrWhiteSpace(quotationExternalId))
        {
            return;
        }

        var conversation = await _dbContext.InternalConversations
            .AsTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    x.RelatedType == InternalMailRelatedType.Quotation &&
                    x.RelatedId == quotationId,
                cancellationToken);

        if (conversation is null)
        {
            return;
        }

        var subject = BuildSubject(quotationExternalId, productCodes);
        if (!string.Equals(conversation.Subject, subject, StringComparison.Ordinal))
        {
            conversation.Subject = subject;
        }
    }
}
