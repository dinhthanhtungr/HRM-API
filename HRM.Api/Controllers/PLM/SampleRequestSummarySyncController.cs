using HRM.Application.Features.PLM.SampleRequests.Commands.SyncConversationSummaries;
using HRM.Application.Features.PLM.SampleRequests.Queries.ConversationSummary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HRM.Api.Controllers.PLM;
[ApiController, Authorize, Route("api/v1/dev/sample-request-summary-sync")]
public sealed class SampleRequestSummarySyncController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var result = await sender.Send(new GetSummarySyncStatusQuery(), ct);
        return result is null ? StatusCode(StatusCodes.Status403Forbidden) : Ok(result);
    }
    [HttpPost]
    public async Task<IActionResult> Queue(SyncConversationSummariesCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result is null ? StatusCode(StatusCodes.Status403Forbidden) : Ok(result);
    }
}
