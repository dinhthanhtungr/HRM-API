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
    IDateTimeProvider dateTimeProvider,
    KeyedMutationLock<Guid> mutationLock)
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

        if (command.PolicyId == Guid.Empty)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail("Pricing policy id is required.");
        }

        using var lease = await mutationLock.AcquireAsync(command.PolicyId, cancellationToken);

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
        var categoryExists = !command.Request.CategoryId.HasValue || await dbContext.Categories.AsNoTracking().AnyAsync(x =>
            x.CategoryId == command.Request.CategoryId.Value && x.CompanyId == companyId && x.IsActive == true,
            cancellationToken);
        if (!categoryExists)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(
                "Category was not found, inactive, or is outside the current company.");
        }

        var configurationError = FormulaPricingPolicyRules.ValidatePolicyConfiguration(
            command.Request.Name,
            entity.Currency,
            command.Request.DefaultManufacturingCost,
            command.Request.DefaultProfitMarginRate,
            command.Request.RoundingRule,
            command.Request.RoundingIncrement,
            command.Request.EffectiveFrom,
            command.Request.PriceValidityDays);
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

        var existingTiersBySortOrder = entity.Tiers.ToDictionary(x => x.SortOrder);
        var requestedSortOrders = tiersResult.Data.Select(x => x.SortOrder).ToHashSet();
        foreach (var requestedTier in tiersResult.Data)
        {
            if (existingTiersBySortOrder.TryGetValue(requestedTier.SortOrder, out var existingTier))
            {
                ApplyTierValues(existingTier, requestedTier);
                continue;
            }

            entity.Tiers.Add(requestedTier);
        }

        var removedTiers = entity.Tiers
            .Where(x => !requestedSortOrders.Contains(x.SortOrder))
            .ToArray();
        dbContext.FormulaPricingPolicyTiers.RemoveRange(removedTiers);
        foreach (var removedTier in removedTiers)
        {
            entity.Tiers.Remove(removedTier);
        }
        entity.CategoryId = command.Request.CategoryId;
        entity.Name = command.Request.Name.Trim();
        entity.DefaultManufacturingCost = command.Request.DefaultManufacturingCost;
        entity.DefaultProfitMarginRate = command.Request.DefaultProfitMarginRate;
        entity.RoundingRule = command.Request.RoundingRule;
        entity.RoundingIncrement = command.Request.RoundingIncrement;
        entity.EffectiveFrom = command.Request.EffectiveFrom;
        entity.PriceValidityDays = command.Request.PriceValidityDays;
        var now = dateTimeProvider.Now;
        entity.Status = FormulaPricingPolicyStatus.Published;
        entity.PublishedBy = employeeId;
        entity.PublishedAt = now;
        entity.UpdatedBy = employeeId;
        entity.UpdatedDate = now;

        var previousPublishedPolicies = await dbContext.FormulaPricingPolicies
            .AsTracking()
            .Where(x =>
                x.FormulaPricingPolicyId != entity.FormulaPricingPolicyId &&
                x.CompanyId == companyId &&
                x.CategoryId == entity.CategoryId &&
                x.Profile == entity.Profile &&
                x.Currency == entity.Currency &&
                x.Status == FormulaPricingPolicyStatus.Published &&
                x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var previousPolicy in previousPublishedPolicies)
        {
            previousPolicy.Status = FormulaPricingPolicyStatus.Superseded;
            previousPolicy.UpdatedBy = employeeId;
            previousPolicy.UpdatedDate = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<FormulaPricingPolicyDto>.Ok(
            FormulaPricingPolicyRules.ToDto(entity),
            "Pricing policy updated and published successfully.");
    }

    private static void ApplyTierValues(
        HRM.Domain.Entities.CustomerSchema.FormulaPricingPolicyTier target,
        HRM.Domain.Entities.CustomerSchema.FormulaPricingPolicyTier source)
    {
        target.QuantityRangeLabel = source.QuantityRangeLabel;
        target.MinQuantity = source.MinQuantity;
        target.MaxQuantity = source.MaxQuantity;
        target.MinInclusive = source.MinInclusive;
        target.MaxInclusive = source.MaxInclusive;
        target.PriceOffset = source.PriceOffset;
        target.IsActive = source.IsActive;
    }

}
