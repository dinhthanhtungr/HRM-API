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

namespace HRM.Application.Features.CRM.Quotations.Commands.PublishFormulaPricingPolicy;

internal sealed class PublishFormulaPricingPolicyCommandHandler(
    ICRMWriteDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    KeyedMutationLock<string> mutationLock)
    : IRequestHandler<PublishFormulaPricingPolicyCommand, OperationResult<FormulaPricingPolicyDto>>
{
    public async Task<OperationResult<FormulaPricingPolicyDto>> Handle(
        PublishFormulaPricingPolicyCommand command,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(currentUser) ||
            currentUser.CompanyId is not { } companyId ||
            currentUser.EmployeeId is not { } employeeId)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(
                "Only President or Developer with company/employee context can publish pricing policies.");
        }

        var policy = await dbContext.FormulaPricingPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.FormulaPricingPolicyId == command.PolicyId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);
        if (policy is null)
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(
                "Pricing policy was not found or is outside the current company.");
        }

        var lockKey = $"{companyId:N}:{policy.CategoryId:N}:{policy.Profile}:{policy.Currency}";
        using var lease = await mutationLock.AcquireAsync(lockKey, cancellationToken);

        var trackedPolicy = await dbContext.FormulaPricingPolicies
            .AsTracking()
            .Include(x => x.Tiers)
            .FirstAsync(x =>
                x.FormulaPricingPolicyId == command.PolicyId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);
        if (trackedPolicy.Version <= 0 ||
            !trackedPolicy.EffectiveFrom.HasValue ||
            trackedPolicy.EffectiveFrom.Value == default ||
            !trackedPolicy.Tiers.Any(x => x.IsActive))
        {
            return OperationResult<FormulaPricingPolicyDto>.Fail(
                "A pricing policy needs an effective date and at least one active tier before publishing.");
        }

        var now = dateTimeProvider.Now;
        var previousPublishedPolicies = await dbContext.FormulaPricingPolicies
            .AsTracking()
            .Where(x =>
                x.FormulaPricingPolicyId != trackedPolicy.FormulaPricingPolicyId &&
                x.CompanyId == companyId &&
                x.CategoryId == trackedPolicy.CategoryId &&
                x.Profile == trackedPolicy.Profile &&
                x.Currency == trackedPolicy.Currency &&
                x.Status == FormulaPricingPolicyStatus.Published &&
                x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var previousPolicy in previousPublishedPolicies)
        {
            previousPolicy.Status = FormulaPricingPolicyStatus.Superseded;
            previousPolicy.UpdatedBy = employeeId;
            previousPolicy.UpdatedDate = now;
        }

        trackedPolicy.Status = FormulaPricingPolicyStatus.Published;
        trackedPolicy.PublishedBy = employeeId;
        trackedPolicy.PublishedAt = now;
        trackedPolicy.UpdatedBy = employeeId;
        trackedPolicy.UpdatedDate = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<FormulaPricingPolicyDto>.Ok(
            FormulaPricingPolicyRules.ToDto(trackedPolicy),
            "Pricing policy published successfully.");
    }
}
