using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Commands.ConfirmCurrentStandardPrice;

public sealed record ConfirmCurrentStandardPriceCommand(
    Guid ProductId,
    ConfirmCurrentStandardPriceRequest Request) : IRequest<OperationResult<PricingReviewVersionDto>>;
