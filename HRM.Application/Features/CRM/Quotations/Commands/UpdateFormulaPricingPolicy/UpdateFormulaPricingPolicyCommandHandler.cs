using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.UpdateFormulaPricingPolicy;

internal sealed class UpdateFormulaPricingPolicyCommandHandler(
    ICRMWriteDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateFormulaPricingPolicyCommand, OperationResult<FormulaPricingPolicyDto>>
{
    public async Task<OperationResult<FormulaPricingPolicyDto>> Handle(
        UpdateFormulaPricingPolicyCommand command,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(currentUser) ||
            currentUser.CompanyId is not { } companyId ||
            currentUser.EmployeeId is not { } employeeId)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(
                "Only President or Developer with company/employee context can manage pricing policies.");
        }

        var entity = await dbContext.FormulaPricingPolicies
            .AsTracking()
            .Include(x => x.Tiers)
            .FirstOrDefaultAsync(x =>
                x.FormulaPricingPolicyId == command.PolicyId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);
        if (entity is null)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(
                "Pricing policy was not found or is outside the current company.");
        }

        if (entity.Status != FormulaPricingPolicyStatus.Draft)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(
                "Only a draft pricing policy can be edited.");
        }

        var conflict = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
            command.Request.ExpectedUpdatedDate,
            entity.UpdatedDate,
            "Pricing policy");
        if (conflict is not null)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(conflict);
        }

        var configurationError = FormulaPricingPolicyRules.ValidatePolicyConfiguration(
            command.Request.Name,
            entity.Currency,
            command.Request.DefaultManufacturingCost,
            command.Request.DefaultProfitMarginRate,
            command.Request.RoundingRule,
            command.Request.RoundingIncrement,
            command.Request.EffectiveFrom);
        if (configurationError is not null)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(configurationError);
        }

        var tiersResult = FormulaPricingPolicyRules.BuildTiers(
            entity.FormulaPricingPolicyId,
            command.Request.Tiers);
        if (!tiersResult.Success || tiersResult.Data is null)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(tiersResult.Message!);
        }

        dbContext.FormulaPricingPolicyTiers.RemoveRange(entity.Tiers);
        entity.Tiers = tiersResult.Data.ToList();
        entity.Name = command.Request.Name.Trim();
        entity.DefaultManufacturingCost = command.Request.DefaultManufacturingCost;
        entity.DefaultProfitMarginRate = command.Request.DefaultProfitMarginRate;
        entity.RoundingRule = command.Request.RoundingRule;
        entity.RoundingIncrement = command.Request.RoundingIncrement;
        entity.EffectiveFrom = command.Request.EffectiveFrom;
        entity.UpdatedBy = employeeId;
        entity.UpdatedDate = dateTimeProvider.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<FormulaPricingPolicyDto>.Ok(
            FormulaPricingPolicyRules.ToDto(entity),
            "Pricing policy draft updated successfully.");
    }
}
