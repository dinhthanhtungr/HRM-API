using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Commands.ApprovePricingVersion;

public sealed record ApprovePricingVersionCommand(
    Guid ProductId,
    Guid VersionId,
    ApprovePricingReviewVersionRequest Request) : IRequest<OperationResult<PricingReviewVersionDto>>;

