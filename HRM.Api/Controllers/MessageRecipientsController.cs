using HRM.Application.Features.MessageRecipients.Queries.PreviewMessageRecipients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/message-recipients")]
public sealed class MessageRecipientsController : ControllerBase
{
    private readonly ISender _sender;

    public MessageRecipientsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
        [FromBody] PreviewMessageRecipientsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }
}
