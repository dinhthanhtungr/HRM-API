using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Gallery;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGallery;

internal sealed class GetSampleRequestGalleryQueryHandler(
    IPLMReadDbContext db, ICustomerVisibilityService visibility)
    : IRequestHandler<GetSampleRequestGalleryQuery, PagedResult<SampleRequestGalleryDto>>
{
    public async Task<PagedResult<SampleRequestGalleryDto>> Handle(
        GetSampleRequestGalleryQuery request, CancellationToken cancellationToken)
    {
        var query = await SampleRequestSummaryQueryRules.BuildAsync(db, visibility, request, cancellationToken);
        var count = await query.CountAsync(cancellationToken);
        var page = await ProjectPage(db, query, request).ToListAsync(cancellationToken);
        var result = page.Select(x =>
        {
            x.Item.AdditiveName = SampleRequestAdditiveHelper.ResolveDisplayName(x.Item.AdditiveName);
            x.Item.CoverImage = x.CoverId.HasValue
                ? SampleRequestGalleryImages.ToDto(x.Item.SampleRequestId, x.CoverId.Value, x.CoverName!)
                : null;
            return x.Item;
        }).ToList();
        return new PagedResult<SampleRequestGalleryDto>(result, count,
            request.NormalizedPageNumber, request.NormalizedPageSize);
    }

    internal static IQueryable<GalleryProjection> ProjectPage(
        IPLMReadDbContext db, IQueryable<SampleRequest> source, GetSampleRequestGalleryQuery request)
    {
        var images = SampleRequestGalleryImages.Eligible(db.AttachmentModels.AsNoTracking());
        return Sort(source, request).ThenBy(x => x.SampleRequestId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new GalleryProjection
            {
                Item = new SampleRequestGalleryDto
                {
                    SampleRequestId = x.SampleRequestId,
                    ExternalId = x.ExternalId,
                    ProductName = x.Product.Name,
                    ColourCode = x.Product.ColourCode,
                    ColorName = x.Product.ColourName,
                    CategoryName = x.Product.Category != null ? x.Product.Category.Name : null,
                    AdditiveName = x.Product.Additive,
                    CustomerName = x.Customer.CustomerName,
                    CustomerExternalId = x.Customer.ExternalId,
                    Status = x.Status,
                    SampleRequestCreatedDate = x.CreatedDate,
                    ImageCount = images.Count(a => a.AttachmentCollectionId == x.AttachmentCollectionId)
                },
                CoverId = images.Where(a => a.AttachmentCollectionId == x.AttachmentCollectionId)
                    .OrderBy(a => a.CreateDate).ThenBy(a => a.AttachmentId)
                    .Select(a => (Guid?)a.AttachmentId).FirstOrDefault(),
                CoverName = images.Where(a => a.AttachmentCollectionId == x.AttachmentCollectionId)
                    .OrderBy(a => a.CreateDate).ThenBy(a => a.AttachmentId)
                    .Select(a => a.FileName).FirstOrDefault()
            });
    }

    private static IOrderedQueryable<SampleRequest> Sort(
        IQueryable<SampleRequest> query, GetSampleRequestGalleryQuery request) =>
        request.NormalizedSortBy?.ToLowerInvariant() switch
        {
            "externalid" => (request.SortDescending ? query.OrderByDescending(x => x.ExternalId) : query.OrderBy(x => x.ExternalId)).ThenByDescending(x => x.CreatedDate),
            "colourcode" => (request.SortDescending ? query.OrderByDescending(x => x.Product.ColourCode) : query.OrderBy(x => x.Product.ColourCode)).ThenByDescending(x => x.CreatedDate),
            "productname" => (request.SortDescending ? query.OrderByDescending(x => x.Product.Name) : query.OrderBy(x => x.Product.Name)).ThenByDescending(x => x.CreatedDate),
            "updateddate" => (request.SortDescending ? query.OrderByDescending(x => x.UpdatedDate) : query.OrderBy(x => x.UpdatedDate)).ThenByDescending(x => x.CreatedDate),
            "createddate" => (request.SortDescending ? query.OrderByDescending(x => x.CreatedDate) : query.OrderBy(x => x.CreatedDate)).ThenByDescending(x => x.UpdatedDate),
            _ => query.OrderBy(x => x.CreatedDate)
        };

    internal sealed class GalleryProjection
    {
        public SampleRequestGalleryDto Item { get; set; } = null!;
        public Guid? CoverId { get; set; }
        public string? CoverName { get; set; }
    }
}
