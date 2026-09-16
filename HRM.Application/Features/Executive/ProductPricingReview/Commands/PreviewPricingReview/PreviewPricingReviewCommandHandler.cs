using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Commands.PreviewPricingReview;

internal sealed class PreviewPricingReviewCommandHandler(ProductPricingReviewCalculator calculator)
    : IRequestHandler<PreviewPricingReviewCommand, OperationResult<PricingReviewPreviewDto>>
{
    public Task<OperationResult<PricingReviewPreviewDto>> Handle(
        PreviewPricingReviewCommand request, CancellationToken cancellationToken)
        => calculator.PreviewAsync(request.ProductId, request.Request, cancellationToken);
}

