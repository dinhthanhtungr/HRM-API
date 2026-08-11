using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestDataChangeRequest;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestDirectPatchNotification;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestFormulaChangeRequest;
using HRM.Application.Features.PLM.SampleRequests.Commands.DecideSampleRequestDataChange;
using HRM.Application.Features.PLM.SampleRequests.Commands.DecideSampleRequestFormulaChange;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDetail;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestFormOptions;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestHistory;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestLookup;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestMessages;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/sample-requests")]
public sealed class SampleRequestsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public SampleRequestsController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] GetSampleRequestSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Lấy báo cáo theo từng lần gửi/thử mẫu để FE trình bày dạng bảng như file theo dõi Lab.
    /// </summary>
    [HttpGet("sample-trials")]
    public async Task<IActionResult> GetSampleTrials(
        [FromQuery] GetSampleRequestSampleTrialsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("form-options")]
    public async Task<IActionResult> GetFormOptions(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSampleRequestFormOptionsQuery(), cancellationToken);

        return Ok(result);
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup(
        [FromQuery] GetSampleRequestLookupQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{sampleRequestId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid sampleRequestId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSampleRequestDetailQuery
        {
            SampleRequestId = sampleRequestId
        }, cancellationToken);

        return result is null
            ? NotFound(new { message = "Sample request not found." })
            : Ok(result);
    }

    [HttpGet("{sampleRequestId:guid}/history")]
    public async Task<IActionResult> GetHistory(
        Guid sampleRequestId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSampleRequestHistoryQuery
        {
            SampleRequestId = sampleRequestId
        }, cancellationToken);

        return result is null
            ? NotFound(new { message = "Sample request not found." })
            : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateSampleRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(
            nameof(GetDetail),
            new { sampleRequestId = result.Data },
            result);
    }

    [HttpPatch("{sampleRequestId:guid}")]
    public async Task<IActionResult> Patch(
        Guid sampleRequestId,
        [FromBody] PatchSampleRequestCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;

        var result = await _sender.Send(command, cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpPost("{sampleRequestId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid sampleRequestId,
        [FromBody] SendSampleRequestMessageCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;

        var result = await _sender.Send(command, cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpGet("{sampleRequestId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid sampleRequestId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSampleRequestMessagesQuery
        {
            SampleRequestId = sampleRequestId
        }, cancellationToken);

        return result is null
            ? NotFound(new { message = "Sample request not found." })
            : Ok(result);
    }

    [HttpPost("{sampleRequestId:guid}/data-change-requests")]
    public async Task<IActionResult> CreateDataChangeRequest(
        Guid sampleRequestId,
        [FromBody] CreateSampleRequestDataChangeRequestCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;

        var result = await _sender.Send(command, cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpPost("{sampleRequestId:guid}/data-change-requests/{messageId:guid}/decision")]
    public async Task<IActionResult> DecideDataChangeRequest(
        Guid sampleRequestId,
        Guid messageId,
        [FromBody] DecideSampleRequestDataChangeCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;
        command.RequestMessageId = messageId;

        var result = await _sender.Send(command, cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpPost("{sampleRequestId:guid}/direct-patch-notifications")]
    public async Task<IActionResult> CreateDirectPatchNotification(
        Guid sampleRequestId,
        [FromBody] CreateSampleRequestDirectPatchNotificationCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;

        var result = await _sender.Send(command, cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpPost("{sampleRequestId:guid}/formula-change-requests")]
    public async Task<IActionResult> CreateFormulaChangeRequest(
        Guid sampleRequestId,
        [FromBody] CreateSampleRequestFormulaChangeRequestCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;

        var result = await _sender.Send(command, cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpPost("{sampleRequestId:guid}/formula-change-requests/{messageId:guid}/decision")]
    public async Task<IActionResult> DecideFormulaChangeRequest(
        Guid sampleRequestId,
        Guid messageId,
        [FromBody] DecideSampleRequestFormulaChangeCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;
        command.RequestMessageId = messageId;

        var result = await _sender.Send(command, cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    private Guid ResolveCurrentUserId()
    {
        var userId = _currentUser.EmployeeId ?? _currentUser.UserId;
        return userId == Guid.Empty ? _currentUser.UserId : userId;
    }
}
