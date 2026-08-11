using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Enums.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Notifications;

/// <summary>
/// API inbox notification cho FE sau khi polling hoặc nhận event SignalR notify.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("feed")]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetFeed(
        [FromQuery] int take = 20,
        [FromQuery] Guid? afterId = null,
        [FromQuery] DateTime? afterCreated = null,
        [FromQuery] string? categoryCode = null,
        [FromQuery] string? eventGroupCode = null,
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

        var result = await _notificationService.GetFeedAsync(
            take,
            afterId,
            afterCreated,
            categoryCode,
            eventGroupCode,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.GetUnreadCountAsync(cancellationToken));
    }

    [HttpGet("unread-summary")]
    public async Task<ActionResult<NotificationUnreadSummaryDto>> GetUnreadSummary(
        CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.GetUnreadSummaryAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _notificationService.GetByIdAsync(id, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await _notificationService.MarkReadAsync(id, cancellationToken);

        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<int>> MarkAllRead(CancellationToken cancellationToken)
    {
        var updated = await _notificationService.MarkAllReadAsync(cancellationToken);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        return await _notificationService.ArchiveCurrentAsync(id, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("{id:guid}/recipients/{employeeId:guid}/remove")]
    public async Task<IActionResult> RemoveRecipient(
        Guid id,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var archived = await _notificationService.ArchiveRecipientAsync(id, employeeId, cancellationToken);

        return archived
            ? NoContent()
            : NotFound();
    }
}
