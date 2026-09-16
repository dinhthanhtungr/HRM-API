using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Commands.UpdatePricingVersion;

internal sealed class UpdatePricingVersionCommandHandler(ProductPricingReviewWriter writer)
    : IRequestHandler<UpdatePricingVersionCommand, OperationResult<PricingReviewVersionDto>>
{
    public Task<OperationResult<PricingReviewVersionDto>> Handle(
        UpdatePricingVersionCommand request, CancellationToken cancellationToken)
        => writer.UpdateAsync(request.ProductId, request.VersionId, request.Request, cancellationToken);
}

