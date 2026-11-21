using HRM.Application.Features.InternalMail.Commands.SendMessage;
using HRM.Application.Features.InternalMail.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.InternalMail;

[ApiController]
[Authorize]
[Route("api/v1/internal-mail/uploads")]
public sealed class InternalMailUploadsController(InternalMailUploadService uploads) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Start([FromBody] StartInternalMailUpload request, CancellationToken ct)
        => ExecuteAsync(async () => Ok(await uploads.StartAsync(request, ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Progress(Guid id, CancellationToken ct)
        => ExecuteAsync(async () => Ok(await uploads.ProgressAsync(id, ct)));

    [HttpPut("{id:guid}/files/{index:int}")]
    [RequestSizeLimit(1_048_576)]
    public Task<IActionResult> Append(Guid id, int index, [FromQuery] long offset, CancellationToken ct)
        => ExecuteAsync(async () => Ok(new { offset = await uploads.AppendAsync(id, index, offset, Request.Body, ct) }));

    [HttpPost("{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, [FromBody] SendInternalMessageCommand command, CancellationToken ct)
        => ExecuteAsync(async () =>
        {
            var result = await uploads.CompleteAsync(id, command, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        });

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => ExecuteAsync(async () => { await uploads.CancelAsync(id, ct); return NoContent(); });

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        Response.Headers.CacheControl = "private, no-store";
        try { return await action(); }
        catch (UnauthorizedAccessException) { return NotFound(); }
        catch (FileNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
