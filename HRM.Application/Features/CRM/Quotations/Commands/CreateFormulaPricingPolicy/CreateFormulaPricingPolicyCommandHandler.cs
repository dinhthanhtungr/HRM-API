using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.CreateFormulaPricingPolicy;

internal sealed class CreateFormulaPricingPolicyCommandHandler(
    ICRMWriteDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreateFormulaPricingPolicyCommand, OperationResult<FormulaPricingPolicyDto>>
{
    public async Task<OperationResult<FormulaPricingPolicyDto>> Handle(
        CreateFormulaPricingPolicyCommand command,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(currentUser))
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(
                "Only President or Developer can manage pricing policies.");
        }

        if (currentUser.CompanyId is not { } companyId ||
            currentUser.EmployeeId is not { } employeeId ||
            companyId == Guid.Empty ||
            employeeId == Guid.Empty ||
            !Enum.IsDefined(command.Request.Profile))
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(
                "Current company/employee and a valid pricing profile are required.");
        }

        var currency = command.Request.Currency?.Trim().ToUpperInvariant() ?? string.Empty;
        var configurationError = FormulaPricingPolicyRules.ValidatePolicyConfiguration(
            command.Request.Name,
            currency,
            command.Request.DefaultManufacturingCost,
            command.Request.DefaultProfitMarginRate,
            command.Request.RoundingRule,
            command.Request.RoundingIncrement,
            command.Request.EffectiveFrom);
        if (configurationError is not null)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(configurationError);
        }

        var draftExists = await dbContext.FormulaPricingPolicies
            .AsNoTracking()
            .AnyAsync(x =>
                x.CompanyId == companyId &&
                x.Profile == command.Request.Profile &&
                x.Currency == currency &&
                x.Status == FormulaPricingPolicyStatus.Draft &&
                x.IsActive,
                cancellationToken);
        if (draftExists)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(
                "A draft pricing policy already exists for this profile and currency.");
        }

        var policyId = Guid.CreateVersion7();
        var tiersResult = FormulaPricingPolicyRules.BuildTiers(policyId, command.Request.Tiers);
        if (!tiersResult.Success || tiersResult.Data is null)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(tiersResult.Message!);
        }

        var latestVersion = await dbContext.FormulaPricingPolicies
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.Profile == command.Request.Profile &&
                x.Currency == currency)
            .MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0;
        var now = dateTimeProvider.Now;
        var entity = new FormulaPricingPolicy
        {
            FormulaPricingPolicyId = policyId,
            CompanyId = companyId,
            Profile = command.Request.Profile,
            Currency = currency,
            Name = command.Request.Name.Trim(),
            Version = FormulaPricingPolicyRules.GetNextVersion(latestVersion),
            DefaultManufacturingCost = command.Request.DefaultManufacturingCost,
            DefaultProfitMarginRate = command.Request.DefaultProfitMarginRate,
            RoundingRule = command.Request.RoundingRule,
            RoundingIncrement = command.Request.RoundingIncrement,
            EffectiveFrom = command.Request.EffectiveFrom,
            Status = FormulaPricingPolicyStatus.Draft,
            IsActive = true,
            CreatedBy = employeeId,
            CreatedDate = now,
            UpdatedBy = employeeId,
            UpdatedDate = now,
            Tiers = tiersResult.Data.ToList()
        };

        await dbContext.FormulaPricingPolicies.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<FormulaPricingPolicyDto>.Ok(
            FormulaPricingPolicyRules.ToDto(entity),
            "Pricing policy draft created successfully.");
    }
}
