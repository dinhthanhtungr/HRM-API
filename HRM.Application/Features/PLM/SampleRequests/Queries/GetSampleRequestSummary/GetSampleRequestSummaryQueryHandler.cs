using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Summary;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary.Models;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;

internal sealed class GetSampleRequestSummaryQueryHandler
    : IRequestHandler<GetSampleRequestSummaryQuery, PagedResult<SampleRequestSummaryDto>>
{
    private readonly IPLMReadDbContext _dbContext;

    public GetSampleRequestSummaryQueryHandler(IPLMReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<SampleRequestSummaryDto>> Handle(
        GetSampleRequestSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var sampleRequestQuery = _dbContext.SampleRequests
            .Where(x => x.IsActive)
            .AsNoTracking()
            .AsQueryable();

        if (request.CompanyId.HasValue)
        {
            sampleRequestQuery = sampleRequestQuery.Where(x => x.CompanyId == request.CompanyId.Value);
        }

        if (request.ProductId.HasValue)
        {
            sampleRequestQuery = sampleRequestQuery.Where(x => x.ProductId == request.ProductId.Value);
        }

        if (request.FromDate.HasValue)
        {
            var fromDate = request.FromDate.Value.Date;
            sampleRequestQuery = sampleRequestQuery.Where(x => x.CreatedDate >= fromDate);
        }

        if (request.ToDate.HasValue)
        {
            var toDateExclusive = request.ToDate.Value.Date.AddDays(1);
            sampleRequestQuery = sampleRequestQuery.Where(x => x.CreatedDate < toDateExclusive);
        }

        if (request.Status.HasValue)
        {
            var status = request.Status.Value.ToString();
            sampleRequestQuery = sampleRequestQuery.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Color))
        {
            var color = request.Color.Trim();
            sampleRequestQuery = sampleRequestQuery.Where(x => x.Product.ColourName == color);
        }

        if (!string.IsNullOrWhiteSpace(request.AdditiveCode))
        {
            var additiveCode = request.AdditiveCode.Trim();
            sampleRequestQuery = sampleRequestQuery.Where(x => x.Product.Additive == additiveCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();

            sampleRequestQuery = sampleRequestQuery.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.Customer.CustomerName.Contains(keyword) ||
                x.Customer.ExternalId.Contains(keyword) ||
                x.CreatedByNavigation != null && (x.CreatedByNavigation.FullName ?? string.Empty).Contains(keyword) ||
                x.Product.CreatedByNavigation != null && (x.Product.CreatedByNavigation.FullName ?? string.Empty).Contains(keyword) ||
                (x.Product.Name ?? string.Empty).Contains(keyword) ||
                ((x.Product.ColourCode ?? string.Empty).Contains(keyword)));
        }

        var sortedQuery = ApplySorting(sampleRequestQuery, request);

        var dtoQuery = sortedQuery.Select(sampleRequest => new SampleRequestSummaryProjection
        {
            ProductId = sampleRequest.ProductId,
            AttachmentCollectionId = sampleRequest.AttachmentCollectionId,
            Summary = new SampleRequestSummaryDto
            {
                SampleRequestId = sampleRequest.SampleRequestId,
                ExternalId = sampleRequest.ExternalId,
                ProductId = sampleRequest.ProductId,

                ProductName = sampleRequest.Product.Name,
                ColorValue = sampleRequest.Product.ColourName,
                ColorDisplayName = sampleRequest.Product.ColourName,
                AdditiveCode = sampleRequest.Product.Additive,
                AdditiveGroupCode = SampleRequestAdditiveHelper.ResolveGroupCode(sampleRequest.Product.Additive),
                AdditiveDisplayName = sampleRequest.Product.Additive,
                ColourCode = sampleRequest.Product.ColourCode,

                Status = sampleRequest.Status,

                CustomerName = sampleRequest.Customer.CustomerName,
                CustomerExternalId = sampleRequest.Customer.ExternalId,

                CreatedBy = sampleRequest.CreatedByNavigation != null
                    ? sampleRequest.CreatedByNavigation.FullName
                    : "-",

                LabName = sampleRequest.Product.CreatedByNavigation != null
                    ? sampleRequest.Product.CreatedByNavigation.FullName
                    : "-",

                EndUserName = sampleRequest.Product.EndUser,

                ProductCreatedDate = sampleRequest.Product.CreatedDate,
                ProductUpdatedDate = sampleRequest.Product.UpdatedDate,

                SampleRequestCreatedDate = sampleRequest.CreatedDate,
                SampleRequestUpdatedDate = sampleRequest.UpdatedDate,

                ExpectedDeliveryDate = sampleRequest.ExpectedDeliveryDate,
                RequestDeliveryDate = sampleRequest.RequestDeliveryDate,
                RealDeliveryDate = sampleRequest.RealDeliveryDate,
                RealPriceQuoteDate = sampleRequest.RealPriceQuoteDate,
                ExpectedPriceQuoteDate = sampleRequest.ExpectedPriceQuoteDate
            }
        });

        var totalCount = await sortedQuery.CountAsync(cancellationToken);

        var pagedRows = await dtoQuery
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        await SampleRequestSummaryRelatedDataLoader.PopulateAsync(
            _dbContext,
            pagedRows,
            cancellationToken);

        return new PagedResult<SampleRequestSummaryDto>(
            pagedRows.Select(x => x.Summary).ToList(),
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private static IQueryable<SampleRequest> ApplySorting(
        IQueryable<SampleRequest> query,
        GetSampleRequestSummaryQuery request)
    {
        return request.NormalizedSortBy switch
        {
            SampleRequestSummarySortFeilds.ExternalId => request.SortDescending
                ? query.OrderByDescending(x => x.ExternalId).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.ExternalId).ThenByDescending(x => x.CreatedDate),

            SampleRequestSummarySortFeilds.ColourCode => request.SortDescending
                ? query.OrderByDescending(x => x.Product.ColourCode).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.Product.ColourCode).ThenByDescending(x => x.CreatedDate),

            SampleRequestSummarySortFeilds.ProductName => request.SortDescending
                ? query.OrderByDescending(x => x.Product.Name).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.Product.Name).ThenByDescending(x => x.CreatedDate),

            SampleRequestSummarySortFeilds.UpdatedDate => request.SortDescending
                ? query.OrderByDescending(x => x.UpdatedDate).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.UpdatedDate).ThenByDescending(x => x.CreatedDate),

            SampleRequestSummarySortFeilds.CreatedDate => request.SortDescending
                ? query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.UpdatedDate)
                : query.OrderBy(x => x.CreatedDate).ThenByDescending(x => x.UpdatedDate),

            _ => query
                .OrderBy(x => x.CreatedDate)
        };
    }

}
