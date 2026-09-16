using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Commands.ApprovePricingVersion;

internal sealed class ApprovePricingVersionCommandHandler(ProductPricingReviewWriter writer)
    : IRequestHandler<ApprovePricingVersionCommand, OperationResult<PricingReviewVersionDto>>
{
    public Task<OperationResult<PricingReviewVersionDto>> Handle(
        ApprovePricingVersionCommand request, CancellationToken cancellationToken)
        => writer.ApproveAsync(request.ProductId, request.VersionId, request.Request, cancellationToken);
}

