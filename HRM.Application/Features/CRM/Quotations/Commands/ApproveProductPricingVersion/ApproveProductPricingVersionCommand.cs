using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.ApproveProductPricingVersion;

public sealed record ApproveProductPricingVersionCommand(
    Guid ProductPricingVersionId,
    ApproveProductPricingVersionRequest Request)
    : IRequest<OperationResult<ProductPricingVersionDto>>;
