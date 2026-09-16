using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Commands.ApprovePricingVersion;
using HRM.Application.Features.Executive.ProductPricingReview.Commands.ConfirmCurrentStandardPrice;
using HRM.Application.Features.Executive.ProductPricingReview.Commands.CreatePricingVersion;
using HRM.Application.Features.Executive.ProductPricingReview.Commands.PreviewPricingReview;
using HRM.Application.Features.Executive.ProductPricingReview.Commands.UpdatePricingVersion;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Queries.GetMaterialPriceHistory;
using HRM.Application.Features.Executive.ProductPricingReview.Queries.GetMaterialPricePreview;
using HRM.Application.Features.Executive.ProductPricingReview.Queries.GetPricingReview;
using HRM.Application.Features.Executive.ProductPricingReview.Queries.GetPricingSourceOptions;
using HRM.Application.Features.Executive.ProductPricingReview.Queries.GetPricingVersions;
using HRM.Application.Features.Executive.ProductPricingReview.Queries.GetRelatedQuotations;
using HRM.Application.Features.Executive.ProductPricingReview.Queries.GetSupplierPrices;
using HRM.Application.Features.Executive.ProductPricingReview.Queries.GetVaLots;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Executive;

[ApiController]
[Authorize(Policy = ExecutivePolicies.ManageProductPricingReview)]
[Route("api/v1/executive/pricing-review/products/{productId:guid}")]
public sealed class ExecutiveProductPricingReviewController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> GetReview(
        Guid productId,
        [FromQuery] string? currency,
        [FromQuery] Guid? quotationId,
        [FromQuery] PricingReviewSourceType? sourceType,
        [FromQuery] Guid? sourceId,
        CancellationToken cancellationToken)
        => SendAsync(new GetPricingReviewQuery(
            productId, currency, quotationId, sourceType, sourceId), cancellationToken);

    [HttpGet("source-options")]
    public Task<IActionResult> GetSourceOptions(
        Guid productId,
        [FromQuery] PricingReviewSourceType? sourceType,
        [FromQuery] string? keyword,
        [FromQuery] string? currency,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 5,
        CancellationToken cancellationToken = default)
        => SendAsync(new GetPricingSourceOptionsQuery(
            productId, sourceType, keyword, currency, pageNumber, pageSize), cancellationToken);

    [HttpGet("sources/{sourceType}/{sourceId:guid}/material-price-preview")]
    public Task<IActionResult> GetMaterialPricePreview(
        Guid productId,
        PricingReviewSourceType sourceType,
        Guid sourceId,
        [FromQuery] string? currency,
        [FromQuery] int limit = 8,
        [FromQuery] string? sortBy = "absoluteDifference",
        [FromQuery] string? sortDirection = "desc",
        CancellationToken cancellationToken = default)
        => SendAsync(new GetMaterialPricePreviewQuery(
            productId, sourceType, sourceId, currency, limit, sortBy, sortDirection), cancellationToken);

    [HttpGet("materials/{materialId:guid}/supplier-prices")]
    public Task<IActionResult> GetSupplierPrices(
        Guid productId,
        Guid materialId,
        [FromQuery] string? currency,
        CancellationToken cancellationToken)
        => SendAsync(new GetSupplierPricesQuery(productId, materialId, currency), cancellationToken);

    [HttpGet("materials/{materialId:guid}/price-history")]
    public Task<IActionResult> GetMaterialPriceHistory(
        Guid productId,
        Guid materialId,
        [FromQuery] string? currency,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
        => SendAsync(new GetMaterialPriceHistoryQuery(
            productId, materialId, currency, pageNumber, pageSize, sortBy, sortDirection), cancellationToken);

    [HttpPost("preview")]
    public Task<IActionResult> Preview(
        Guid productId,
        [FromBody] PricingReviewPreviewRequest request,
        CancellationToken cancellationToken)
        => SendAsync(new PreviewPricingReviewCommand(productId, request), cancellationToken);

    [HttpGet("versions")]
    public Task<IActionResult> GetVersions(
        Guid productId,
        [FromQuery] string? currency,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
        => SendAsync(new GetPricingVersionsQuery(
            productId, currency, pageNumber, pageSize, sortBy, sortDirection), cancellationToken);

    [HttpGet("related-quotations")]
    public Task<IActionResult> GetRelatedQuotations(
        Guid productId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
        => SendAsync(new GetRelatedQuotationsQuery(
            productId, pageNumber, pageSize, sortBy, sortDirection), cancellationToken);

    [HttpGet("va-lots")]
    public Task<IActionResult> GetVaLots(
        Guid productId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
        => SendAsync(new GetVaLotsQuery(
            productId, pageNumber, pageSize, sortBy, sortDirection), cancellationToken);

    [HttpPost("versions")]
    public Task<IActionResult> CreateVersion(
        Guid productId,
        [FromBody] CreatePricingReviewVersionRequest request,
        CancellationToken cancellationToken)
        => SendAsync(new CreatePricingVersionCommand(productId, request), cancellationToken);

    [HttpPut("versions/{versionId:guid}")]
    public Task<IActionResult> UpdateVersion(
        Guid productId,
        Guid versionId,
        [FromBody] UpdatePricingReviewVersionRequest request,
        CancellationToken cancellationToken)
        => SendAsync(new UpdatePricingVersionCommand(productId, versionId, request), cancellationToken);

    [HttpPost("versions/{versionId:guid}/approve")]
    public Task<IActionResult> ApproveVersion(
        Guid productId,
        Guid versionId,
        [FromBody] ApprovePricingReviewVersionRequest request,
        CancellationToken cancellationToken)
        => SendAsync(new ApprovePricingVersionCommand(productId, versionId, request), cancellationToken);

    [HttpPost("confirm-current-standard-price")]
    public Task<IActionResult> ConfirmCurrentStandardPrice(
        Guid productId,
        [FromBody] ConfirmCurrentStandardPriceRequest request,
        CancellationToken cancellationToken)
        => SendAsync(new ConfirmCurrentStandardPriceCommand(productId, request), cancellationToken);

    private async Task<IActionResult> SendAsync<TResponse>(
        IRequest<OperationResult<TResponse>> request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
