using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.CRM.Quotations.Commands.CreateQuotation;
using HRM.Application.Features.CRM.Quotations.Commands.MarkQuotationSent;
using HRM.Application.Features.CRM.Quotations.Commands.RefreshQuotationPrices;
using HRM.Application.Features.CRM.Quotations.Commands.ReplaceQuotationLines;
using HRM.Application.Features.CRM.Quotations.Commands.RequestQuotation;
using HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotation;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Queries.ExportQuotationPdf;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationById;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationPricingComparison;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricing;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricingOptions;
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
        return result.Success ? Ok(result.Data) : MutationFailure(result);
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
        return result.Success ? Ok(result) : MutationFailure(result);
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
        return result.Success ? Ok(result) : MutationFailure(result);
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
        return result.Success ? NoContent() : MutationFailure(result);
    }

    [HttpPost("{quotationId:guid}/request")]
    public async Task<IActionResult> RequestQuotation(
        Guid quotationId,
        [FromBody] RequestQuotationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RequestQuotationCommand(quotationId, request),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetQuotations(
        [FromQuery] GetQuotationsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("product-pricing-options")]
    public async Task<IActionResult> GetProductPricingOptions(
        [FromQuery] GetQuotationProductPricingOptionsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("products/{productId:guid}/pricing")]
    public async Task<IActionResult> GetProductPricing(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetQuotationProductPricingQuery(productId),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("{quotationId:guid}/pricing-comparison")]
    public async Task<IActionResult> GetQuotationPricingComparison(
        Guid quotationId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetQuotationPricingComparisonQuery(quotationId),
            cancellationToken);
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

    [HttpGet("{quotationId:guid}/pdf")]
    public async Task<IActionResult> ExportQuotationPdf(
        Guid quotationId,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ExportQuotationPdfQuery(quotationId),
            cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return NotFound(result);
        }

        if (download)
        {
            return File(
                result.Data.Content,
                result.Data.ContentType,
                result.Data.FileName);
        }

        Response.Headers.ContentDisposition =
            $"inline; filename*=UTF-8''{Uri.EscapeDataString(result.Data.FileName)}";
        return File(result.Data.Content, result.Data.ContentType);
    }

    private ActionResult MutationFailure(HRM.Application.Commons.Models.OperationResult result)
        => OptimisticConcurrencyHelper.IsConflictMessage(result.Message)
            ? Conflict(result)
            : BadRequest(result);
}
