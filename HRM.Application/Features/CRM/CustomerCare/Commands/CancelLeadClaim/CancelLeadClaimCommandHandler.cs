using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CancelLeadClaim;

internal sealed class CancelLeadClaimCommandHandler
    : IRequestHandler<CancelLeadClaimCommand, OperationResult>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CancelLeadClaimCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult> Handle(
        CancelLeadClaimCommand command,
        CancellationToken cancellationToken)
    {
        if (command.CustomerId == Guid.Empty || command.ClaimId == Guid.Empty)
        {
            return OperationResult.Fail("CustomerId or ClaimId is invalid.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var canAccess = await _visibilityService
            .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
            .AnyAsync(customer =>
                customer.CustomerId == command.CustomerId &&
                customer.IsLead,
                cancellationToken);
        if (!canAccess)
        {
            return OperationResult.Fail("Lead was not found or is outside your visibility scope.");
        }

        var claim = await _writeDbContext.CustomerClaims
            .FirstOrDefaultAsync(item =>
                item.Id == command.ClaimId &&
                item.CustomerId == command.CustomerId &&
                item.CompanyId == scope.CompanyId &&
                item.Type == ClaimType.Work &&
                item.IsActive,
                cancellationToken);
        if (claim is null)
        {
            return OperationResult.Fail("Lead claim was not found.");
        }

        var canCancel =
            scope.HasFullCustomerView ||
            claim.EmployeeId == scope.EmployeeId ||
            scope.LeaderGroupIds.Contains(claim.GroupId);
        if (!canCancel)
        {
            return OperationResult.Fail("You do not have permission to cancel this lead claim.");
        }

        claim.IsActive = false;

        var customer = await _writeDbContext.Customers
            .FirstOrDefaultAsync(item =>
                item.CustomerId == command.CustomerId &&
                item.CompanyId == scope.CompanyId,
                cancellationToken);
        if (customer is not null)
        {
            customer.UpdatedBy = scope.EmployeeId;
            customer.UpdatedDate = _dateTimeProvider.Now;
        }

        try
        {
            await _writeDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult.Fail(BuildConcurrencyMessage("Lead claim", exception));
        }

        return OperationResult.Ok("Lead caretaker claim cancelled successfully.");
    }

    private static string BuildConcurrencyMessage(string featureName, DbUpdateConcurrencyException exception)
    {
        var entityNames = exception.Entries
            .Select(entry => entry.Entity.GetType().Name)
            .Distinct()
            .ToList();

        var suffix = entityNames.Count == 0
            ? string.Empty
            : $" Affected entities: {string.Join(", ", entityNames)}.";

        return $"{featureName} data was changed or deleted by another process. Please reload before saving.{suffix}";
    }
}
