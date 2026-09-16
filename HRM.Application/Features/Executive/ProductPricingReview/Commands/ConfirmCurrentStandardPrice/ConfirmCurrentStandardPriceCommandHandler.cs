using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Commands.ConfirmCurrentStandardPrice;

internal sealed class ConfirmCurrentStandardPriceCommandHandler(ProductPricingReviewWriter writer)
    : IRequestHandler<ConfirmCurrentStandardPriceCommand, OperationResult<PricingReviewVersionDto>>
{
    public Task<OperationResult<PricingReviewVersionDto>> Handle(
        ConfirmCurrentStandardPriceCommand request,
        CancellationToken cancellationToken)
        => writer.ConfirmCurrentAsync(request.ProductId, request.Request, cancellationToken);
}
