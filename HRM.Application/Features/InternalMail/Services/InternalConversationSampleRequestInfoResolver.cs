using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>
/// Loads current Sample Request presentation metadata in batch for authorized InternalMail conversations.
/// </summary>
internal sealed class InternalConversationSampleRequestInfoResolver
{
    private readonly IInternalMailDbContext _dbContext;

    public InternalConversationSampleRequestInfoResolver(IInternalMailDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<Guid, SampleRequestConversationInfoDto>> ResolveAsync(
        IEnumerable<Guid> sampleRequestIds,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var ids = sampleRequestIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (ids.Length == 0 || companyId == Guid.Empty)
        {
            return new Dictionary<Guid, SampleRequestConversationInfoDto>();
        }

        var items = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(sampleRequest =>
                ids.Contains(sampleRequest.SampleRequestId) &&
                sampleRequest.CompanyId == companyId &&
                sampleRequest.IsActive &&
                sampleRequest.Customer.CompanyId == companyId &&
                sampleRequest.ManagerByNavigation.CompanyId == companyId)
            .Select(sampleRequest => new SampleRequestConversationInfoDto
            {
                SampleRequestId = sampleRequest.SampleRequestId,
                RequestCode = sampleRequest.ExternalId,
                ColourCode = sampleRequest.Product.ColourCode,
                ProductCategory = sampleRequest.Product.CompanyId == companyId &&
                                  sampleRequest.Product.IsActive &&
                                  sampleRequest.Product.Category != null &&
                                  sampleRequest.Product.Category.CompanyId == companyId &&
                                  sampleRequest.Product.Category.IsActive == true &&
                                  sampleRequest.Product.Category.Types == "Product"
                    ? new ConversationProductCategoryDto
                    {
                        CategoryId = sampleRequest.Product.Category.CategoryId,
                        Code = sampleRequest.Product.Category.ExternalId,
                        Name = sampleRequest.Product.Category.Name
                    }
                    : null,
                CustomerId = sampleRequest.CustomerId,
                CustomerCode = sampleRequest.Customer.ExternalId,
                CustomerName = sampleRequest.Customer.CustomerName,
                SaleEmployeeId = sampleRequest.ManagerBy,
                SaleName = sampleRequest.ManagerByNavigation.FullName
            })
            .ToListAsync(cancellationToken);

        return items.ToDictionary(item => item.SampleRequestId);
    }
}
