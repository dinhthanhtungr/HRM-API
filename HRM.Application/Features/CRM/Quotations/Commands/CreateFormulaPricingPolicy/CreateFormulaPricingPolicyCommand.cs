using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.CreateFormulaPricingPolicy;

public sealed record CreateFormulaPricingPolicyCommand(
    CreateFormulaPricingPolicyRequest Request)
    : IRequest<OperationResult<FormulaPricingPolicyDto>>;
