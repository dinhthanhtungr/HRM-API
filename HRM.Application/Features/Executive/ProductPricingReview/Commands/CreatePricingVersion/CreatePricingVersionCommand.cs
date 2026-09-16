using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Commands.CreatePricingVersion;

public sealed record CreatePricingVersionCommand(
    Guid ProductId,
    CreatePricingReviewVersionRequest Request) : IRequest<OperationResult<PricingReviewVersionDto>>;

