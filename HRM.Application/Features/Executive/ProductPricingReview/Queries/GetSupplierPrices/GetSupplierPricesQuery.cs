using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetSupplierPrices;

public sealed record GetSupplierPricesQuery(
    Guid ProductId,
    Guid MaterialId,
    string? Currency) : IRequest<OperationResult<PricingReviewSupplierPricesDto>>;

