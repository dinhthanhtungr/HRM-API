using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Commands.CreatePricingVersion;

internal sealed class CreatePricingVersionCommandHandler(ProductPricingReviewWriter writer)
    : IRequestHandler<CreatePricingVersionCommand, OperationResult<PricingReviewVersionDto>>
{
    public Task<OperationResult<PricingReviewVersionDto>> Handle(
        CreatePricingVersionCommand request, CancellationToken cancellationToken)
        => writer.CreateAsync(request.ProductId, request.Request, cancellationToken);
}

