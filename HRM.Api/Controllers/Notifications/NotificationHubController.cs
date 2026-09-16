using HRM.Application.Features.NotificationHub.Dtos;
using HRM.Application.Features.NotificationHub.Queries.GetItems;
using HRM.Domain.Enums.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Notifications;

/// <summary>
/// Read API tổng hợp dành riêng cho danh sách Notification Hub.
/// Không thay thế API ghi/trạng thái của Notification hoặc Internal Mail.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/notification-hub")]
public sealed class NotificationHubController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationHubController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lấy notification theo category/event group và enrich metadata của đúng các
    /// conversation xuất hiện trong trang feed. urgentOnly=true tạo tab Gấp, có thể
    /// kết hợp với mọi category/event group. Không tải toàn bộ inbox Internal Mail.
    /// </summary>
    [HttpGet("items")]
    [ProducesResponseType(typeof(NotificationHubItemsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationHubItemsDto>> GetItems(
        [FromQuery] string? categoryCode = null,
        [FromQuery] string? eventGroupCode = null,
        [FromQuery] bool urgentOnly = false,
        [FromQuery] int take = 20,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (categoryCode is not null && !NotificationTopicCatalog.IsKnownCategory(categoryCode))
        {
            return BadRequest($"Unknown notification categoryCode '{categoryCode}'.");
        }

        if (eventGroupCode is not null && !NotificationTopicCatalog.IsKnownEventGroup(eventGroupCode))
        {
            return BadRequest($"Unknown notification eventGroupCode '{eventGroupCode}'.");
        }

        if (NotificationTopicCatalog.NormalizeCode(categoryCode) == NotificationCategoryCodes.LegacyData &&
            eventGroupCode is not null)
        {
            return BadRequest("legacy_data does not support eventGroupCode filtering.");
        }

        if (!NotificationHubCursor.TryDecode(cursor, out var afterCreated, out var afterId))
        {
            return BadRequest("Invalid notification hub cursor.");
        }

        return Ok(await _sender.Send(new GetNotificationHubItemsQuery
        {
            CategoryCode = categoryCode,
            EventGroupCode = eventGroupCode,
            UrgentOnly = urgentOnly,
            Take = take,
            AfterCreated = string.IsNullOrWhiteSpace(cursor) ? null : afterCreated,
            AfterId = string.IsNullOrWhiteSpace(cursor) ? null : afterId
        }, cancellationToken));
    }
}
