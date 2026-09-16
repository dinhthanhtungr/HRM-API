using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Commands.PreviewPricingReview;

public sealed record PreviewPricingReviewCommand(
    Guid ProductId,
    PricingReviewPreviewRequest Request) : IRequest<OperationResult<PricingReviewPreviewDto>>;

