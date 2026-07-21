using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Dtos.FormOptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestLookup;

internal sealed class GetSampleRequestLookupQueryHandler
    : IRequestHandler<GetSampleRequestLookupQuery, IReadOnlyList<SampleRequestLookupItemDto>>
{
    private readonly IPLMReadDbContext _dbContext;

    public GetSampleRequestLookupQueryHandler(IPLMReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SampleRequestLookupItemDto>> Handle(
        GetSampleRequestLookupQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SampleRequests
            .AsNoTracking()
            .AsQueryable();

        if (request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        if (request.CompanyId is { } companyId && companyId != Guid.Empty)
        {
            query = query.Where(x => x.CompanyId == companyId);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedStatus))
        {
            var status = request.NormalizedStatus;
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;

            query = query.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.Customer.ExternalId.Contains(keyword) ||
                x.Customer.CustomerName.Contains(keyword) ||
                (x.Product.ColourCode ?? string.Empty).Contains(keyword) ||
                (x.Product.Name ?? string.Empty).Contains(keyword) ||
                (x.Product.Code ?? string.Empty).Contains(keyword));
        }

        return await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.SampleRequestId)
            .Take(request.NormalizedTake)
            .Select(x => new SampleRequestLookupItemDto
            {
                SampleRequestId = x.SampleRequestId,
                ExternalId = x.ExternalId,
                Status = x.Status,
                CustomerId = x.CustomerId,
                CustomerExternalId = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName,
                ProductId = x.ProductId,
                ProductCode = x.Product.ColourCode ?? x.Product.Code,
                ProductName = x.Product.Name,
                CreatedDate = x.CreatedDate,
                Label = x.ExternalId + " - " + x.Customer.CustomerName + " - " + (x.Product.Name ?? x.Product.ColourCode ?? x.Product.Code ?? string.Empty)
            })
            .ToListAsync(cancellationToken);
    }
}
