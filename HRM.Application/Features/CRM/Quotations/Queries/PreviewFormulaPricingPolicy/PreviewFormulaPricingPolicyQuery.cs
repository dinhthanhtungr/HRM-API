using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.PreviewFormulaPricingPolicy;

public sealed record PreviewFormulaPricingPolicyQuery(
    Guid PolicyId,
    PreviewFormulaPricingPolicyRequest Request)
    : IRequest<OperationResult<FormulaPriceCalculationDto>>;
