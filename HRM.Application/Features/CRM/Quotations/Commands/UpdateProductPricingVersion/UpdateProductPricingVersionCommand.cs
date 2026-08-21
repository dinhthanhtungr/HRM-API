using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.UpdateProductPricingVersion;

public sealed record UpdateProductPricingVersionCommand(
    Guid ProductPricingVersionId,
    UpdateProductPricingVersionRequest Request)
    : IRequest<OperationResult<ProductPricingVersionDto>>;
