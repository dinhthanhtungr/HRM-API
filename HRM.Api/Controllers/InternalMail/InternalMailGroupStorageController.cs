using HRM.Application.Features.InternalMail.Commands.SynchronizeGroupStorage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.InternalMail;

// GROUP-STORAGE-TRANSITION: synchronization-api — delete this controller after migration acceptance, retain the live writer.
[ApiController]
[Authorize]
[Route("api/v1/internal-mail/group-storage")]
public sealed class InternalMailGroupStorageController(ISender sender) : ControllerBase
{
    [HttpPost("synchronize")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> Synchronize(CancellationToken ct, [FromQuery] string? target = null, [FromQuery] bool? preview = null)
    {
        if ((target is not (null or "mapping" or "all")) || preview.HasValue)
            return BadRequest(new { code = "UnsupportedGroupSynchronizationTarget", message = "Use all (mapping + President membership) or mapping; preview and policy membership synchronization are not available." });
        var result = await sender.Send(new SynchronizeGroupStorageCommand { IncludePresidentMemberships = target != "mapping" }, ct);
        if (result is null) return Forbid();
        Response.Headers["X-Group-Sync-Outcome"] = result.Outcome;
        Response.Headers["X-Group-Sync-Run-Id"] = result.RunId.ToString("D");
        Response.Headers.CacheControl = "no-store";
        return File(result.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.FileName);
    }
}
