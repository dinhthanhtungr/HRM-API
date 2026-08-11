using HRM.Application.Features.Timeline.Queries.GetSaleOrderTimeline;
using HRM.Application.Features.Timeline.Queries.GetSaleOrderTimelineDetail;
using HRM.Application.Features.Timeline.Queries.GetTimelineBySource;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Timeline;

[ApiController]
[Authorize]
[Route("api/v1/timeline")]
public sealed class TimelineController : ControllerBase
{
    private readonly ISender _sender;

    public TimelineController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("plm/sale-orders")]
    public async Task<IActionResult> GetSaleOrderTimeline(
        [FromQuery] GetSaleOrderTimelineQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("plm/sale-orders/{merchandiseOrderId:guid}/details")]
    public async Task<IActionResult> GetSaleOrderTimelineDetail(
        [FromRoute] Guid merchandiseOrderId,
        [FromQuery] GetSaleOrderTimelineDetailQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSaleOrderTimelineDetailQuery
        {
            Id = merchandiseOrderId,
            Status = query.Status,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            Keyword = query.Keyword,
            SortBy = query.SortBy,
            SortDirection = query.SortDirection
        }, cancellationToken);
        return Ok(result);
    }

    [HttpGet("sources/{sourceId:guid}")]
    public async Task<IActionResult> GetTimelineBySource(
        [FromRoute] Guid sourceId,
        [FromQuery] GetTimelineBySourceQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTimelineBySourceQuery
        {
            SourceId = sourceId,
            EventType = query.EventType,
            Status = query.Status,
            CreatedBy = query.CreatedBy,
            From = query.From,
            To = query.To,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            Keyword = query.Keyword,
            SortBy = query.SortBy,
            SortDirection = query.SortDirection
        }, cancellationToken);
        return Ok(result);
    }
}
