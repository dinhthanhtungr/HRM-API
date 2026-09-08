using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Commands.MigrateProductCategories;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestSampleTrial;
using HRM.Application.Features.PLM.SampleRequests.Commands.ConfirmSampleRequestSampleReceipt;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestDataChangeRequest;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestDirectPatchNotification;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestFormulaChangeRequest;
using HRM.Application.Features.PLM.SampleRequests.Commands.DecideSampleRequestDataChange;
using HRM.Application.Features.PLM.SampleRequests.Commands.DecideSampleRequestFormulaChange;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequestSampleTrial;
using HRM.Application.Features.PLM.SampleRequests.Commands.RecordSampleRequestSampleTrialCustomerFeedback;
using HRM.Application.Features.PLM.SampleRequests.Commands.RecordSampleTrialCustomerFeedbackInteraction;
using HRM.Application.Features.PLM.SampleRequests.Commands.RequestSampleRequestPriceQuote;
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
    /// Lấy danh sách theo Sample Request, kèm Trial mới nhất để Sale theo dõi tiến độ Lab.
    /// </summary>
    [HttpGet("sample-trials")]
    public async Task<IActionResult> GetSampleTrials(
        [FromQuery] GetSampleRequestSampleTrialsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Lấy lịch sử các lần Trial của một Sample Request; FE chỉ gọi khi người dùng mở phần lịch sử.
    /// </summary>
    [HttpGet("{sampleRequestId:guid}/sample-trials")]
    public async Task<IActionResult> GetSampleTrialHistory(
        Guid sampleRequestId,
        [FromQuery] GetSampleRequestSampleTrialsQuery query,
        CancellationToken cancellationToken)
    {
        query.SampleRequestId = sampleRequestId;
        query.IncludeTrialHistory = true;

        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Tạo lần thử/gửi mẫu tiếp theo; backend tự cấp TrialNo và snapshot dữ liệu báo cáo.
    /// </summary>
    [HttpPost("{sampleRequestId:guid}/sample-trials")]
    public async Task<IActionResult> CreateSampleTrial(
        Guid sampleRequestId,
        [FromBody] CreateSampleRequestSampleTrialCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;
        var result = await _sender.Send(command, cancellationToken);

        return result.Success
            ? CreatedAtAction(nameof(GetSampleTrials), new { sampleRequestId }, result)
            : BadRequest(result);
    }

    /// <summary>
    /// Cập nhật một trial; field nằm trong clearFields được chuyển thành null.
    /// </summary>
    [HttpPatch("{sampleRequestId:guid}/sample-trials/{trialId:guid}")]
    public async Task<IActionResult> PatchSampleTrial(
        Guid sampleRequestId,
        Guid trialId,
        [FromBody] PatchSampleRequestSampleTrialCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;
        command.SampleRequestSampleTrialId = trialId;
        var result = await _sender.Send(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Sale xác nhận đã nhận mẫu từ action trong Notification Hub; nếu không gửi ngày nhận, backend dùng thời điểm hiện tại.
    /// </summary>
    [HttpPost("{sampleRequestId:guid}/sample-trials/{trialId:guid}/confirm-receipt")]
    public async Task<IActionResult> ConfirmSampleReceipt(
        Guid sampleRequestId,
        Guid trialId,
        [FromBody] ConfirmSampleRequestSampleReceiptCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;
        command.SampleRequestSampleTrialId = trialId;
        var result = await _sender.Send(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Sale cập nhật phản hồi của Trial và tạo CRM interaction liên kết trong cùng một transaction.
    /// </summary>
    [HttpPost("{sampleRequestId:guid}/sample-trials/{trialId:guid}/customer-feedback")]
    public async Task<IActionResult> RecordSampleTrialCustomerFeedbackInteraction(
        Guid sampleRequestId,
        Guid trialId,
        [FromBody] RecordSampleTrialCustomerFeedbackInteractionCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;
        command.SampleRequestSampleTrialId = trialId;
        var result = await _sender.Send(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Sale records customer feedback for a sent sample. Terminal outcomes advance
    /// the trial, sample request, and selected formula in one business action.
    /// </summary>
    [HttpPost("{sampleRequestId:guid}/customer-feedback")]
    public async Task<IActionResult> RecordSampleTrialCustomerFeedback(
        Guid sampleRequestId,
        [FromBody] RecordSampleRequestSampleTrialCustomerFeedbackCommand command,
        CancellationToken cancellationToken)
    {
        command.SampleRequestId = sampleRequestId;
        var result = await _sender.Send(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("form-options")]
    public async Task<IActionResult> GetFormOptions(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSampleRequestFormOptionsQuery(), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Chuyển Product active của công ty hiện tại sang CMP/PIG theo rule chuẩn hoá đã duyệt.
    /// </summary>
    [HttpPost("products/migrate-categories")]
    [Authorize(Policy = PlmPolicies.EditProductTechnicalInfo)]
    public async Task<IActionResult> MigrateProductCategories(
        [FromQuery] bool dryRun = true,
        [FromQuery] string? targetCategoryCode = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new MigrateProductCategoriesCommand
            {
                DryRun = dryRun,
                TargetCategoryCode = targetCategoryCode
            },
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
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
        CancellationToken cancellationToken,
        [FromQuery] bool forSaleOrder = false,
        [FromQuery] Guid? customerId = null,
        [FromQuery] HRM.Domain.Enums.Merchadises.OrderType? orderType = null)
    {
        var result = await _sender.Send(new GetSampleRequestDetailQuery
        {
            SampleRequestId = sampleRequestId,
            ForSaleOrder = forSaleOrder,
            CustomerId = customerId,
            OrderType = orderType
        }, cancellationToken);

        if (result is not null)
        {
            return Ok(result);
        }

        var accessStatus = await _sender.Send(new GetSampleRequestDetailAccessQuery
        {
            SampleRequestId = sampleRequestId
        }, cancellationToken);

        return accessStatus switch
        {
            SampleRequestDetailAccessStatus.Forbidden => StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    code = "sample_request_forbidden",
                    message = "You do not have permission to read this sample request."
                }),
            SampleRequestDetailAccessStatus.InvalidRelationship => UnprocessableEntity(new
            {
                code = "sample_request_invalid_relationship",
                message = "Sample request is not linked to an active customer and product in the current company."
            }),
            _ => NotFound(new
            {
                code = "sample_request_not_found",
                message = "Sample request not found."
            })
        };
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

    /// <summary>
    /// Gửi yêu cầu báo giá vào thread của Sample Request. Nếu FE không chỉ định Formula,
    /// backend ưu tiên Formula của trial active mới nhất rồi đến Formula trên Sample Request.
    /// </summary>
    [HttpPost("{sampleRequestId:guid}/price-quote-requests")]
    public async Task<IActionResult> RequestPriceQuote(
        Guid sampleRequestId,
        [FromBody] RequestSampleRequestPriceQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RequestSampleRequestPriceQuoteCommand(sampleRequestId, request),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
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
