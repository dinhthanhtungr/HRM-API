using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGallery;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGalleryImages;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGalleryImageContent;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize(Policy = HRM.Api.Security.Authorization.SensitiveDataAuthorization.PolicyName)]
[Route("api/v1/plm/sample-requests")]
public sealed class SampleRequestGalleryController(ISender sender) : ControllerBase
{
    [HttpGet("gallery")]
    public async Task<IActionResult> List([FromQuery] GetSampleRequestGalleryQuery query, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await sender.Send(query, ct));
    }

    [HttpGet("{sampleRequestId:guid}/gallery-images")]
    public async Task<IActionResult> Images(Guid sampleRequestId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await sender.Send(new GetSampleRequestGalleryImagesQuery(sampleRequestId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{sampleRequestId:guid}/attachments/{attachmentId:guid}/thumbnail")]
    public Task<IActionResult> Thumbnail(Guid sampleRequestId, Guid attachmentId, CancellationToken ct) =>
        Content(sampleRequestId, attachmentId, true, ct);

    [HttpGet("{sampleRequestId:guid}/attachments/{attachmentId:guid}/gallery-content")]
    public Task<IActionResult> Original(Guid sampleRequestId, Guid attachmentId, CancellationToken ct) =>
        Content(sampleRequestId, attachmentId, false, ct);

    private async Task<IActionResult> Content(Guid requestId, Guid attachmentId, bool thumbnail, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var result = await sender.Send(new GetSampleRequestGalleryImageContentQuery(requestId, attachmentId, thumbnail), ct);
        return result is null ? NotFound() : File(result.Stream, result.ContentType, enableRangeProcessing: !thumbnail);
    }
}
