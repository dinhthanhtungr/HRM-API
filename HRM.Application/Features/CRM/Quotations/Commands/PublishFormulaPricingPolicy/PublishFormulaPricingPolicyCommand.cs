using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.PublishFormulaPricingPolicy;

public sealed record PublishFormulaPricingPolicyCommand(
    Guid PolicyId,
    PublishFormulaPricingPolicyRequest Request)
    : IRequest<OperationResult<FormulaPricingPolicyDto>>;
