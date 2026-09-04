using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.CRM.Quotations.Commands.CreateQuotation;
using HRM.Application.Features.CRM.Quotations.Commands.CreateProductPricingVersion;
using HRM.Application.Features.CRM.Quotations.Commands.UpdateProductPricingVersion;
using HRM.Application.Features.CRM.Quotations.Commands.ApproveProductPricingVersion;
using HRM.Application.Features.CRM.Quotations.Commands.MarkQuotationSent;
using HRM.Application.Features.CRM.Quotations.Commands.RefreshQuotationPrices;
using HRM.Application.Features.CRM.Quotations.Commands.ReplaceQuotationLines;
using HRM.Application.Features.CRM.Quotations.Commands.RequestQuotation;
using HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotation;
using HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotationCustomerPriceTiers;
using HRM.Application.Features.CRM.Quotations.Commands.WithdrawQuotationPricing;
using HRM.Application.Features.CRM.Quotations.Commands.CreateFormulaPricingPolicy;
using HRM.Application.Features.CRM.Quotations.Commands.PublishFormulaPricingPolicy;
using HRM.Application.Features.CRM.Quotations.Commands.UpdateFormulaPricingPolicy;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Queries.ExportQuotationPdf;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationById;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationCustomerTerms;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationPricingComparison;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationPricingWorkspace;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationPricingQueue;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricing;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricingOptions;
using HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingWorkbench;
using HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingWorkbenchDetail;
using HRM.Application.Features.CRM.Quotations.Queries.GetQuotations;
using HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingVersions;
using HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingSources;
using HRM.Application.Features.CRM.Quotations.Queries.GetFormulaPricingPolicies;
using HRM.Application.Features.CRM.Quotations.Queries.PreviewFormulaPricingPolicy;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
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

    [HttpPut("{quotationId:guid}/customer-price-tiers")]
    public async Task<IActionResult> UpdateCustomerPriceTiers(
        Guid quotationId,
        [FromBody] UpdateQuotationCustomerPriceTiersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateQuotationCustomerPriceTiersCommand(quotationId, request),
            cancellationToken);
        return result.Success ? Ok(result.Data) : MutationFailure(result);
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

    [HttpPost("{quotationId:guid}/withdraw-pricing-request")]
    public async Task<IActionResult> WithdrawQuotationPricingRequest(
        Guid quotationId,
        [FromBody] WithdrawQuotationPricingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new WithdrawQuotationPricingRequestCommand(quotationId, request),
            cancellationToken);
        return result.Success ? Ok(result.Data) : MutationFailure(result);
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

    [HttpGet("product-pricing-workbench")]
    public async Task<IActionResult> GetProductPricingWorkbench(
        [FromQuery] GetProductPricingWorkbenchQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("products/{productId:guid}/pricing-workbench")]
    public async Task<IActionResult> GetProductPricingWorkbenchDetail(
        Guid productId,
        [FromQuery] string? currency,
        [FromQuery] ProductPricingSourceType? sourceType,
        [FromQuery] Guid? sourceId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetProductPricingWorkbenchDetailQuery(
                productId,
                currency,
                sourceType,
                sourceId),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("pricing-queue")]
    public async Task<IActionResult> GetQuotationPricingQueue(
        [FromQuery] GetQuotationPricingQueueQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("products/{productId:guid}/pricing")]
    public async Task<IActionResult> GetProductPricing(
        Guid productId,
        [FromQuery] string? currency,
        [FromQuery] Guid? customerId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetQuotationProductPricingQuery(productId, currency, customerId),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("product-pricing-versions")]
    public async Task<IActionResult> GetProductPricingVersions(
        [FromQuery] Guid productId,
        [FromQuery] string? currency,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetProductPricingVersionsQuery(productId, currency), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("products/{productId:guid}/pricing-sources")]
    public async Task<IActionResult> GetProductPricingSources(
        Guid productId,
        [FromQuery] string currency,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetProductPricingSourcesQuery(productId, currency), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("product-pricing-versions")]
    public async Task<IActionResult> CreateProductPricingVersion(
        [FromBody] CreateProductPricingVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateProductPricingVersionCommand(request), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPut("product-pricing-versions/{productPricingVersionId:guid}")]
    public async Task<IActionResult> UpdateProductPricingVersion(
        Guid productPricingVersionId,
        [FromBody] UpdateProductPricingVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateProductPricingVersionCommand(productPricingVersionId, request), cancellationToken);
        return result.Success ? Ok(result.Data) : MutationFailure(result);
    }

    [HttpPost("product-pricing-versions/{productPricingVersionId:guid}/approve")]
    public async Task<IActionResult> ApproveProductPricingVersion(
        Guid productPricingVersionId,
        [FromBody] ApproveProductPricingVersionRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ApproveProductPricingVersionCommand(
                productPricingVersionId,
                request ?? new ApproveProductPricingVersionRequest()),
            cancellationToken);
        return result.Success ? Ok(result.Data) : MutationFailure(result);
    }

    [HttpGet("pricing-policies")]
    public async Task<IActionResult> GetPricingPolicies(
        [FromQuery] Guid? categoryId,
        [FromQuery] FormulaPricingProfile? profile,
        [FromQuery] string? currency,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetFormulaPricingPoliciesQuery(categoryId, profile, currency), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("pricing-policies")]
    public async Task<IActionResult> CreatePricingPolicy(
        [FromBody] CreateFormulaPricingPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateFormulaPricingPolicyCommand(request), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPut("pricing-policies/{policyId:guid}")]
    public async Task<IActionResult> UpdatePricingPolicy(
        Guid policyId, [FromBody] UpdateFormulaPricingPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateFormulaPricingPolicyCommand(policyId, request), cancellationToken);
        return result.Success ? Ok(result.Data) : MutationFailure(result);
    }

    [HttpPost("pricing-policies/{policyId:guid}/publish")]
    public async Task<IActionResult> PublishPricingPolicy(
        Guid policyId, [FromBody] PublishFormulaPricingPolicyRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new PublishFormulaPricingPolicyCommand(
            policyId, request ?? new PublishFormulaPricingPolicyRequest()), cancellationToken);
        return result.Success ? Ok(result.Data) : MutationFailure(result);
    }

    [HttpPost("pricing-policies/{policyId:guid}/preview")]
    public async Task<IActionResult> PreviewPricingPolicy(
        Guid policyId, [FromBody] PreviewFormulaPricingPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PreviewFormulaPricingPolicyQuery(policyId, request), cancellationToken);
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

    [HttpGet("{quotationId:guid}/pricing-workspace")]
    public async Task<IActionResult> GetQuotationPricingWorkspace(
        Guid quotationId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetQuotationPricingWorkspaceQuery(quotationId),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("customer-terms")]
    public async Task<IActionResult> GetCustomerTerms(
        [FromQuery] Guid customerId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetQuotationCustomerTermsQuery(customerId),
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
