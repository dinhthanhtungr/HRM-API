using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.PreviewFormulaPricingPolicy;

public sealed record PreviewFormulaPricingPolicyQuery(
    Guid PolicyId,
    PreviewFormulaPricingPolicyRequest Request)
    : IRequest<OperationResult<FormulaPriceCalculationDto>>;

internal sealed class PreviewFormulaPricingPolicyQueryHandler(
    ICRMReadDbContext dbContext,
    ICurrentUser currentUser)
    : IRequestHandler<PreviewFormulaPricingPolicyQuery, OperationResult<FormulaPriceCalculationDto>>
{
    public async Task<OperationResult<FormulaPriceCalculationDto>> Handle(
        PreviewFormulaPricingPolicyQuery query,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(currentUser) ||
            currentUser.CompanyId is not { } companyId)
            return OperationResult<FormulaPriceCalculationDto>.Fail(
                "Only President or Developer can preview pricing policies.");
        if (query.Request.MaterialCost < 0m || query.Request.ManufacturingCost < 0m ||
            query.Request.StandardSellingPrice < 0m)
            return OperationResult<FormulaPriceCalculationDto>.Fail(
                "Preview prices and costs cannot be negative.");

        var policy = await dbContext.FormulaPricingPolicies.AsNoTracking()
            .Include(x => x.Tiers)
            .FirstOrDefaultAsync(x =>
                x.FormulaPricingPolicyId == query.PolicyId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);
        if (policy is null)
            return OperationResult<FormulaPriceCalculationDto>.Fail(
                "Pricing policy was not found or is outside the current company.");

        var result = FormulaPriceCalculator.Calculate(
            FormulaPricingPolicyRules.ToDefinition(policy),
            query.Request.MaterialCost,
            query.Request.ManufacturingCost,
            query.Request.StandardSellingPrice);
        return OperationResult<FormulaPriceCalculationDto>.Ok(result);
    }
}
