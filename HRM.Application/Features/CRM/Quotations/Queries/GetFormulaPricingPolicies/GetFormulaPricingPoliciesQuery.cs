using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.Formulas;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetFormulaPricingPolicies;

public sealed record GetFormulaPricingPoliciesQuery(
    Guid? CategoryId,
    FormulaPricingProfile? Profile,
    string? Currency)
    : IRequest<OperationResult<IReadOnlyList<FormulaPricingPolicyDto>>>;
