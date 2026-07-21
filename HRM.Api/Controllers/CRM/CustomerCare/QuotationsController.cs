using HRM.Application.Features.CRM.Quotations.Commands.CreateQuotation;
using HRM.Application.Features.CRM.Quotations.Commands.MarkQuotationSent;
using HRM.Application.Features.CRM.Quotations.Commands.RefreshQuotationPrices;
using HRM.Application.Features.CRM.Quotations.Commands.ReplaceQuotationLines;
using HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotation;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationById;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.CRM.CustomerCare;

/// <summary>
/// API báo giá theo luồng nháp, cập nhật lại giá và ghi nhận đã gửi khách hàng.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/crm/quotations")]
public sealed class QuotationsController : ControllerBase
{
    private readonly ISender _sender;

    public QuotationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateQuotation(
        [FromBody] CreateQuotationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateQuotationCommand(request), cancellationToken);
        return result.Success && result.Data is not null
            ? CreatedAtAction(
                nameof(GetQuotationById),
                new { quotationId = result.Data.QuotationId },
                result)
            : BadRequest(result);
    }

    [HttpPatch("{quotationId:guid}")]
    public async Task<IActionResult> UpdateQuotation(
        Guid quotationId,
        [FromBody] UpdateQuotationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateQuotationCommand(quotationId, request),
            cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpPut("{quotationId:guid}/lines")]
    public async Task<IActionResult> ReplaceQuotationLines(
        Guid quotationId,
        [FromBody] ReplaceQuotationLinesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReplaceQuotationLinesCommand(quotationId, request),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{quotationId:guid}/refresh-prices")]
    public async Task<IActionResult> RefreshQuotationPrices(
        Guid quotationId,
        [FromBody] RefreshQuotationPricesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RefreshQuotationPricesCommand(quotationId, request),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{quotationId:guid}/mark-sent")]
    public async Task<IActionResult> MarkQuotationSent(
        Guid quotationId,
        [FromBody] MarkQuotationSentRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new MarkQuotationSentCommand(quotationId, request ?? new MarkQuotationSentRequest()),
            cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetQuotations(
        [FromQuery] GetQuotationsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("{quotationId:guid}")]
    public async Task<IActionResult> GetQuotationById(
        Guid quotationId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetQuotationByIdQuery(quotationId), cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(result);
    }
}
