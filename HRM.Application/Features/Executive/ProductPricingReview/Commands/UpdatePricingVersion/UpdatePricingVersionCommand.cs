using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Commands.UpdatePricingVersion;

public sealed record UpdatePricingVersionCommand(
    Guid ProductId,
    Guid VersionId,
    UpdatePricingReviewVersionRequest Request) : IRequest<OperationResult<PricingReviewVersionDto>>;

