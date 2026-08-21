using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.CreateProductPricingVersion;

public sealed record CreateProductPricingVersionCommand(CreateProductPricingVersionRequest Request)
    : IRequest<OperationResult<ProductPricingVersionDto>>;
