using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Gallery;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGallery;

/// <summary>Danh sách ảnh gọn, dùng cùng bộ lọc và phạm vi đọc của Summary.</summary>
public sealed class GetSampleRequestGalleryQuery : SampleRequestSummaryFilters,
    IRequest<PagedResult<SampleRequestGalleryDto>>, IValidatableObject
{
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext)) yield return result;
        if (NormalizedPageNumber > int.MaxValue / NormalizedPageSize)
            yield return new ValidationResult("Page number is outside the supported range.", [nameof(PageNumber)]);
    }
}
