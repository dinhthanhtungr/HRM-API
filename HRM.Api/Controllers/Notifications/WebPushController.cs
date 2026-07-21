using HRM.Application.Features.Notifications.Commands.RegisterWebPushSubscription;
using HRM.Application.Features.Notifications.Commands.UnsubscribeWebPush;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Queries.GetWebPushPublicKey;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Notifications;

/// <summary>
/// API dang ky Web Push cho browser hien tai. Endpoint/key subscription khong duoc tra ve sau khi luu.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/web-push")]
public sealed class WebPushController : ControllerBase
{
    private readonly ISender _sender;

    public WebPushController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("public-key")]
    public async Task<IActionResult> GetPublicKey(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetWebPushPublicKeyQuery(), cancellationToken);
        return result.Success ? Ok(result.Data) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }

    [HttpPost("subscriptions")]
    public async Task<IActionResult> Subscribe(
        [FromBody] RegisterWebPushSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        command.UserAgent = Request.Headers.UserAgent.ToString();
        var result = await _sender.Send(command, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("subscriptions/unsubscribe")]
    public async Task<IActionResult> Unsubscribe(
        [FromBody] UnsubscribeWebPushRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UnsubscribeWebPushCommand
        {
            Endpoint = request.Endpoint
        }, cancellationToken);

        return result.Success ? NoContent() : NotFound(result);
    }
}
