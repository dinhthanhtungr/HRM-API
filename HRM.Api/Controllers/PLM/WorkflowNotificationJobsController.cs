using HRM.Application.Features.PLM.SampleRequests.Commands.RetryWorkflowNotificationJob;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetWorkflowNotificationJobs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/dev/workflow-notification-jobs")]
public sealed class WorkflowNotificationJobsController(ISender sender) : ControllerBase
{
    public sealed record RecoverReadyBody(string? SampleRequestCode);

    [HttpPost("recover-ready")]
    public async Task<IActionResult> RecoverReady([FromBody] RecoverReadyBody body, CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new RecoverReadyWorkflowNotificationsCommand(body.SampleRequestCode), ct);
            return result is null ? Forbid() : Ok(result);
        }
        catch (ArgumentException) { return BadRequest(new { code = "InvalidFilter" }); }
    }

    [HttpGet("{jobId:long}/silent-watchers")]
    public async Task<IActionResult> PreviewWatchers(long jobId, CancellationToken ct)
    {
        var result = await sender.Send(new HRM.Application.Features.PLM.SampleRequests.Queries.PreviewSilentWatchers.PreviewSilentWatchersQuery(jobId), ct);
        return result is null ? Forbid() : result.Outcome == "Preview" ? Ok(result) : Conflict(result);
    }

    public sealed record RestoreWatchersBody(string Fingerprint);

    [HttpPost("{jobId:long}/silent-watchers/restore")]
    public async Task<IActionResult> RestoreWatchers(long jobId, [FromBody] RestoreWatchersBody body, CancellationToken ct)
    {
        var result = await sender.Send(new HRM.Application.Features.PLM.SampleRequests.Commands.RestoreSilentWatchers.RestoreSilentWatchersCommand(jobId, body.Fingerprint), ct);
        return result is null ? Forbid() : result.Outcome is "Restored" or "AlreadyCurrent" ? Ok(result) : Conflict(result);
    }
    [HttpPost("{jobId:long}/restore-summary")]
    public async Task<IActionResult> RestoreSummary(long jobId, CancellationToken ct)
    {
        var result = await sender.Send(new HRM.Application.Features.PLM.SampleRequests.Commands.RestoreWorkflowSummary.RestoreWorkflowSummaryCommand(jobId), ct);
        return result.Outcome switch
        {
            "Forbidden" => Forbid(),
            "Restored" or "AlreadyRestored" => Ok(result),
            _ => Conflict(result)
        };
    }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? sampleRequestCode, [FromQuery] string status = "Attention",
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var result = await sender.Send(new GetWorkflowNotificationJobsQuery(sampleRequestCode, status, pageNumber, pageSize), ct);
            return result is null ? Forbid() : Ok(result);
        }
        catch (ArgumentException) { return BadRequest(new { code = "InvalidFilter" }); }
    }

    [HttpPost("{jobId:long}/retry")]
    public async Task<IActionResult> Retry(long jobId, CancellationToken ct)
    {
        var result = await sender.Send(new RetryWorkflowNotificationJobCommand(jobId), ct);
        return result.Outcome switch
        {
            "Forbidden" => Forbid(),
            "Unavailable" or "RecoveryConflict" => Conflict(result),
            _ => Ok(result)
        };
    }
}
