using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.UpdateFormulaPricingPolicy;

public sealed record UpdateFormulaPricingPolicyCommand(
    Guid PolicyId,
    UpdateFormulaPricingPolicyRequest Request)
    : IRequest<OperationResult<FormulaPricingPolicyDto>>;
