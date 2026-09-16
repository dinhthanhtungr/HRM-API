using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>
/// Loads current quotation presentation metadata in batch for authorized InternalMail conversations.
/// </summary>
internal sealed class InternalConversationQuotationInfoResolver
{
    private readonly IInternalMailDbContext _dbContext;

    public InternalConversationQuotationInfoResolver(IInternalMailDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<Guid, QuotationConversationInfoDto>> ResolveAsync(
        IEnumerable<Guid> quotationIds,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var ids = quotationIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (ids.Length == 0 || companyId == Guid.Empty)
        {
            return new Dictionary<Guid, QuotationConversationInfoDto>();
        }

        var items = await _dbContext.Quotations
            .AsNoTracking()
            .Where(quotation =>
                ids.Contains(quotation.QuotationId) &&
                quotation.CompanyId == companyId &&
                quotation.IsActive &&
                quotation.Customer.CompanyId == companyId &&
                quotation.SaleEmployee.CompanyId == companyId)
            .Select(quotation => new QuotationConversationInfoDto
            {
                QuotationId = quotation.QuotationId,
                QuotationCode = quotation.ExternalId,
                CustomerId = quotation.CustomerId,
                CustomerCode = quotation.Customer.ExternalId,
                CustomerName = quotation.Customer.CustomerName,
                SaleEmployeeId = quotation.SaleEmployeeId,
                SaleName = quotation.SaleEmployee.FullName
            })
            .ToListAsync(cancellationToken);

        return items.ToDictionary(item => item.QuotationId);
    }
}
