using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.ManageFormulaPricingPolicy;

public sealed record CreateFormulaPricingPolicyCommand(CreateFormulaPricingPolicyRequest Request)
    : IRequest<OperationResult<FormulaPricingPolicyDto>>;
public sealed record UpdateFormulaPricingPolicyCommand(Guid PolicyId, UpdateFormulaPricingPolicyRequest Request)
    : IRequest<OperationResult<FormulaPricingPolicyDto>>;
public sealed record PublishFormulaPricingPolicyCommand(Guid PolicyId, PublishFormulaPricingPolicyRequest Request)
    : IRequest<OperationResult<FormulaPricingPolicyDto>>;

internal sealed class CreateFormulaPricingPolicyCommandHandler(
    ICRMWriteDbContext dbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreateFormulaPricingPolicyCommand, OperationResult<FormulaPricingPolicyDto>>
{
    public async Task<OperationResult<FormulaPricingPolicyDto>> Handle(CreateFormulaPricingPolicyCommand command, CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(currentUser))
            return OperationResult<FormulaPricingPolicyDto>.Fail("Only President or Developer can manage pricing policies.");
        if (currentUser.CompanyId is not { } companyId || currentUser.EmployeeId is not { } employeeId ||
            companyId == Guid.Empty || employeeId == Guid.Empty || !Enum.IsDefined(command.Request.Profile))
            return OperationResult<FormulaPricingPolicyDto>.Fail("Current company/employee and a valid pricing profile are required.");

        var currency = string.IsNullOrWhiteSpace(command.Request.Currency) ? "VND" : command.Request.Currency.Trim().ToUpperInvariant();
        if (currency.Length > 10)
            return OperationResult<FormulaPricingPolicyDto>.Fail("Currency cannot exceed 10 characters.");
        if (await dbContext.FormulaPricingPolicies.AsNoTracking().AnyAsync(x => x.CompanyId == companyId &&
            x.Profile == command.Request.Profile && x.Currency == currency &&
            x.Status == FormulaPricingPolicyStatus.Draft && x.IsActive, cancellationToken))
            return OperationResult<FormulaPricingPolicyDto>.Fail("A draft pricing policy already exists for this profile and currency.");

        var defaults = FormulaPriceCalculator.GetDefaultPolicy(command.Request.Profile);
        var tierRequests = command.Request.Tiers is { Count: > 0 } ? command.Request.Tiers : defaults.Tiers.Select(x =>
            new FormulaPricingPolicyTierRequest { QuantityRangeLabel = x.QuantityRangeLabel, MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity, MinInclusive = x.MinInclusive, MaxInclusive = x.MaxInclusive,
                PriceOffset = x.PriceOffset, SortOrder = x.SortOrder }).ToArray();
        var id = Guid.CreateVersion7();
        var tiersResult = FormulaPricingPolicyRules.BuildTiers(id, tierRequests);
        if (!tiersResult.Success || tiersResult.Data is null) return OperationResult<FormulaPricingPolicyDto>.Fail(tiersResult.Message!);
        var latestVersion = await dbContext.FormulaPricingPolicies.AsNoTracking().Where(x => x.CompanyId == companyId &&
            x.Profile == command.Request.Profile && x.Currency == currency).MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0;
        var now = dateTimeProvider.Now;
        var entity = new FormulaPricingPolicy { FormulaPricingPolicyId = id, CompanyId = companyId,
            Profile = command.Request.Profile, Currency = currency,
            Name = string.IsNullOrWhiteSpace(command.Request.Name) ? $"{command.Request.Profile} pricing policy v{latestVersion + 1}" : command.Request.Name.Trim(),
            Version = latestVersion + 1, DefaultManufacturingCost = command.Request.DefaultManufacturingCost ?? defaults.DefaultManufacturingCost,
            Status = FormulaPricingPolicyStatus.Draft, IsActive = true, CreatedBy = employeeId, CreatedDate = now,
            UpdatedBy = employeeId, UpdatedDate = now, Tiers = tiersResult.Data.ToList() };
        if (entity.DefaultManufacturingCost < 0m || entity.Name.Length > 150)
            return OperationResult<FormulaPricingPolicyDto>.Fail("Policy name is limited to 150 characters and manufacturing cost cannot be negative.");
        await dbContext.FormulaPricingPolicies.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<FormulaPricingPolicyDto>.Ok(FormulaPricingPolicyRules.ToDto(entity), "Pricing policy draft created successfully.");
    }
}

internal sealed class UpdateFormulaPricingPolicyCommandHandler(
    ICRMWriteDbContext dbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateFormulaPricingPolicyCommand, OperationResult<FormulaPricingPolicyDto>>
{
    public async Task<OperationResult<FormulaPricingPolicyDto>> Handle(UpdateFormulaPricingPolicyCommand command, CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(currentUser) || currentUser.CompanyId is not { } companyId || currentUser.EmployeeId is not { } employeeId)
            return OperationResult<FormulaPricingPolicyDto>.Fail("Only President or Developer with company/employee context can manage pricing policies.");
        var entity = await dbContext.FormulaPricingPolicies.AsTracking().Include(x => x.Tiers)
            .FirstOrDefaultAsync(x => x.FormulaPricingPolicyId == command.PolicyId && x.CompanyId == companyId && x.IsActive, cancellationToken);
        if (entity is null) return OperationResult<FormulaPricingPolicyDto>.Fail("Pricing policy was not found or is outside the current company.");
        if (entity.Status != FormulaPricingPolicyStatus.Draft) return OperationResult<FormulaPricingPolicyDto>.Fail("Only a draft pricing policy can be edited.");
        var conflict = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(command.Request.ExpectedUpdatedDate, entity.UpdatedDate, "Pricing policy");
        if (conflict is not null) return OperationResult<FormulaPricingPolicyDto>.Fail(conflict);
        if (string.IsNullOrWhiteSpace(command.Request.Name) || command.Request.Name.Trim().Length > 150 || command.Request.DefaultManufacturingCost < 0m)
            return OperationResult<FormulaPricingPolicyDto>.Fail("A name up to 150 characters and a non-negative manufacturing cost are required.");
        var tiersResult = FormulaPricingPolicyRules.BuildTiers(entity.FormulaPricingPolicyId, command.Request.Tiers);
        if (!tiersResult.Success || tiersResult.Data is null) return OperationResult<FormulaPricingPolicyDto>.Fail(tiersResult.Message!);
        dbContext.FormulaPricingPolicyTiers.RemoveRange(entity.Tiers);
        entity.Tiers = tiersResult.Data.ToList(); entity.Name = command.Request.Name.Trim();
        entity.DefaultManufacturingCost = command.Request.DefaultManufacturingCost; entity.UpdatedBy = employeeId;
        entity.UpdatedDate = dateTimeProvider.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<FormulaPricingPolicyDto>.Ok(FormulaPricingPolicyRules.ToDto(entity), "Pricing policy draft updated successfully.");
    }
}

internal sealed class PublishFormulaPricingPolicyCommandHandler(
    ICRMWriteDbContext dbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider,
    KeyedMutationLock<string> mutationLock)
    : IRequestHandler<PublishFormulaPricingPolicyCommand, OperationResult<FormulaPricingPolicyDto>>
{
    public async Task<OperationResult<FormulaPricingPolicyDto>> Handle(PublishFormulaPricingPolicyCommand command, CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(currentUser) || currentUser.CompanyId is not { } companyId || currentUser.EmployeeId is not { } employeeId)
            return OperationResult<FormulaPricingPolicyDto>.Fail("Only President or Developer with company/employee context can publish pricing policies.");
        var entity = await dbContext.FormulaPricingPolicies.AsNoTracking().FirstOrDefaultAsync(x =>
            x.FormulaPricingPolicyId == command.PolicyId && x.CompanyId == companyId && x.IsActive, cancellationToken);
        if (entity is null) return OperationResult<FormulaPricingPolicyDto>.Fail("Pricing policy was not found or is outside the current company.");
        using var lease = await mutationLock.AcquireAsync($"{companyId:N}:{entity.Profile}:{entity.Currency}", cancellationToken);
        var tracked = await dbContext.FormulaPricingPolicies.AsTracking().Include(x => x.Tiers).FirstAsync(x => x.FormulaPricingPolicyId == command.PolicyId, cancellationToken);
        if (tracked.Status != FormulaPricingPolicyStatus.Draft || tracked.Tiers.Count == 0)
            return OperationResult<FormulaPricingPolicyDto>.Fail("Only a complete draft pricing policy can be published.");
        var conflict = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(command.Request.ExpectedUpdatedDate, tracked.UpdatedDate, "Pricing policy");
        if (conflict is not null) return OperationResult<FormulaPricingPolicyDto>.Fail(conflict);
        var now = dateTimeProvider.Now;
        var previous = await dbContext.FormulaPricingPolicies.AsTracking().Where(x => x.FormulaPricingPolicyId != tracked.FormulaPricingPolicyId &&
            x.CompanyId == companyId && x.Profile == tracked.Profile && x.Currency == tracked.Currency &&
            x.Status == FormulaPricingPolicyStatus.Published && x.IsActive).ToListAsync(cancellationToken);
        foreach (var item in previous) { item.Status = FormulaPricingPolicyStatus.Superseded; item.UpdatedBy = employeeId; item.UpdatedDate = now; }
        tracked.Status = FormulaPricingPolicyStatus.Published; tracked.EffectiveFrom = now; tracked.PublishedBy = employeeId;
        tracked.PublishedAt = now; tracked.UpdatedBy = employeeId; tracked.UpdatedDate = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<FormulaPricingPolicyDto>.Ok(FormulaPricingPolicyRules.ToDto(tracked), "Pricing policy published successfully.");
    }
}
