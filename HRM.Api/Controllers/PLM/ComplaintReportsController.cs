using System.Text.Json;
using System.Text.Json.Serialization;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Commands.CreateComplaintReport;
using HRM.Application.Features.PLM.ComplaintReports.Commands.FinalDecision;
using HRM.Application.Features.PLM.ComplaintReports.Commands.InitialDecision;
using HRM.Application.Features.PLM.ComplaintReports.Commands.ReplaceCapaActions;
using HRM.Application.Features.PLM.ComplaintReports.Commands.RequestVerification;
using HRM.Application.Features.PLM.ComplaintReports.Commands.Resubmit;
using HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateCapaActionResult;
using HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateEffectiveness;
using HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateInvestigation;
using HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateReception;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.ComplaintReports.Queries.GetBySourceOrder;
using HRM.Application.Features.PLM.ComplaintReports.Queries.GetComplaintReportById;
using HRM.Application.Features.PLM.ComplaintReports.Queries.GetComplaintReports;
using HRM.Application.Features.PLM.ComplaintReports.Queries.GetSourceLines;
using HRM.Application.Features.PLM.ComplaintReports.Queries.ExportPdf;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

/// <summary>
/// Manages customer complaint reception, CAPA workflow, approvals and PDF export.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/plm/complaint-reports")]
public sealed class ComplaintReportsController : ControllerBase
{
    private static readonly JsonSerializerOptions MultipartJsonOptions = CreateMultipartJsonOptions();
    private readonly ISender _sender;

    public ComplaintReportsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Creates and submits a complaint with optional evidence files.</summary>
    [HttpPost]
    [Authorize(Policy = ComplaintPolicies.Create)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(200_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 200_000_000)]
    public async Task<IActionResult> Create(
        [FromForm(Name = "request")] string requestJson,
        [FromForm(Name = "files")] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        CreateComplaintReportRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<CreateComplaintReportRequest>(
                requestJson,
                MultipartJsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Field request không phải JSON hợp lệ." });
        }

        if (request is null)
        {
            return BadRequest(new { message = "Thiếu dữ liệu complaint report." });
        }

        var uploadFiles = (files ?? new List<IFormFile>())
            .Select(file => new AttachmentUploadFile(
                file.OpenReadStream(),
                file.FileName,
                file.ContentType,
                file.Length))
            .ToList();
        try
        {
            var result = await _sender.Send(
                new CreateComplaintReportCommand(request, uploadFiles),
                cancellationToken);
            return result.Success
                ? StatusCode(StatusCodes.Status201Created, result)
                : BadRequest(result);
        }
        finally
        {
            foreach (var uploadFile in uploadFiles)
            {
                await uploadFile.Stream.DisposeAsync();
            }
        }
    }

    /// <summary>Returns delivered SaleOrder lines and lots eligible for complaint.</summary>
    [HttpGet("source-lines")]
    public async Task<IActionResult> GetSourceLines(
        [FromQuery] GetComplaintSourceLinesQuery query,
        CancellationToken cancellationToken)
        => Ok(await _sender.Send(query, cancellationToken));

    /// <summary>Returns the paged complaint list within customer visibility.</summary>
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] GetComplaintReportsQuery query,
        CancellationToken cancellationToken)
        => Ok(await _sender.Send(query, cancellationToken));

    /// <summary>Returns complaint badges and statuses for a source SaleOrder timeline.</summary>
    [HttpGet("by-source-order/{merchandiseOrderId:guid}")]
    public async Task<IActionResult> GetBySourceOrder(
        [FromRoute] Guid merchandiseOrderId,
        CancellationToken cancellationToken)
        => Ok(await _sender.Send(
            new GetComplaintReportsBySourceOrderQuery(merchandiseOrderId),
            cancellationToken));

    /// <summary>Returns complete complaint, CAPA, approval, handling-order and allowed-action details.</summary>
    [HttpGet("{complaintReportId:guid}")]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid complaintReportId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetComplaintReportByIdQuery(complaintReportId), cancellationToken);
        return result is null ? NotFound(new { message = "Complaint report not found." }) : Ok(result);
    }

    /// <summary>Renders the VA-QMR-F31(05) CAPA report as an inline PDF.</summary>
    [HttpGet("{complaintReportId:guid}/pdf")]
    [Authorize(Policy = ComplaintPolicies.ViewPdf)]
    [Produces("application/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromRoute] Guid complaintReportId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ExportComplaintReportPdfQuery(complaintReportId),
            cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return NotFound(result);
        }

        Response.Headers.ContentDisposition =
            $"inline; filename*=UTF-8''{Uri.EscapeDataString(result.Data.FileName)}";
        return File(result.Data.Content, result.Data.ContentType);
    }

    /// <summary>Atomically replaces the sale-owned reception of a returned Draft complaint.</summary>
    [HttpPut("{complaintReportId:guid}/reception")]
    [Authorize(Policy = ComplaintPolicies.Create)]
    public async Task<IActionResult> UpdateReception(
        [FromRoute] Guid complaintReportId,
        [FromBody] UpdateComplaintReceptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateComplaintReceptionCommand(complaintReportId, request),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Records the initial HOD decision and creates replacement production when approved.</summary>
    [HttpPost("{complaintReportId:guid}/initial-decision")]
    [Authorize(Policy = ComplaintPolicies.InitialApprove)]
    public async Task<IActionResult> InitialDecision(
        [FromRoute] Guid complaintReportId,
        [FromBody] InitialComplaintDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new InitialComplaintDecisionCommand(complaintReportId, request),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Resubmits a corrected Draft complaint to initial approval.</summary>
    [HttpPost("{complaintReportId:guid}/resubmit")]
    [Authorize(Policy = ComplaintPolicies.Create)]
    public async Task<IActionResult> Resubmit(
        [FromRoute] Guid complaintReportId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ResubmitComplaintReportCommand(complaintReportId),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Records the final HOD decision to close or return a complaint.</summary>
    [HttpPost("{complaintReportId:guid}/final-decision")]
    [Authorize(Policy = ComplaintPolicies.FinalApprove)]
    public async Task<IActionResult> FinalDecision(
        [FromRoute] Guid complaintReportId,
        [FromBody] FinalComplaintDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new FinalComplaintDecisionCommand(complaintReportId, request),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Updates investigation, related standards and risk review.</summary>
    [HttpPut("{complaintReportId:guid}/investigation")]
    [Authorize(Policy = ComplaintPolicies.Investigate)]
    public async Task<IActionResult> UpdateInvestigation(
        [FromRoute] Guid complaintReportId,
        [FromBody] UpdateComplaintInvestigationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateComplaintInvestigationCommand(complaintReportId, request),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Atomically replaces immediate and corrective/preventive actions.</summary>
    [HttpPut("{complaintReportId:guid}/capa-actions")]
    [Authorize(Policy = ComplaintPolicies.ActionUpdate)]
    public async Task<IActionResult> ReplaceCapaActions(
        [FromRoute] Guid complaintReportId,
        [FromBody] ReplaceComplaintCapaActionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReplaceComplaintCapaActionsCommand(complaintReportId, request),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Updates only the assigned CAPA action result and completion time.</summary>
    [HttpPatch("{complaintReportId:guid}/capa-actions/{actionId:guid}/result")]
    [Authorize(Policy = ComplaintPolicies.ActionUpdate)]
    public async Task<IActionResult> UpdateCapaActionResult(
        [FromRoute] Guid complaintReportId,
        [FromRoute] Guid actionId,
        [FromBody] UpdateComplaintCapaActionResultRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateComplaintCapaActionResultCommand(complaintReportId, actionId, request),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Moves completed CAPA actions to effectiveness verification.</summary>
    [HttpPost("{complaintReportId:guid}/request-verification")]
    [Authorize(Policy = ComplaintPolicies.Investigate)]
    public async Task<IActionResult> RequestVerification(
        [FromRoute] Guid complaintReportId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RequestComplaintVerificationCommand(complaintReportId),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Records effectiveness verification and requests final approval.</summary>
    [HttpPut("{complaintReportId:guid}/effectiveness")]
    [Authorize(Policy = ComplaintPolicies.Verify)]
    public async Task<IActionResult> UpdateEffectiveness(
        [FromRoute] Guid complaintReportId,
        [FromBody] UpdateComplaintEffectivenessRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateComplaintEffectivenessCommand(complaintReportId, request),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private static JsonSerializerOptions CreateMultipartJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
