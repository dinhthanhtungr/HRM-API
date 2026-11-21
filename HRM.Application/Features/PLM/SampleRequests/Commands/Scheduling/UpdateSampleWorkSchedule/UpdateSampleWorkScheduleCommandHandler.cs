using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.UpdateSampleWorkSchedule;

/// <summary>Lab planning does not accept formulas, feedback or lifecycle status, and never publishes workflow notifications.</summary>
internal sealed class UpdateSampleWorkScheduleCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility, IDateTimeProvider clock)
    : IRequestHandler<UpdateSampleWorkScheduleCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(UpdateSampleWorkScheduleCommand command, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule) ||
            user.CompanyId is not { } company || company == Guid.Empty || user.EmployeeId is not { } actor || actor == Guid.Empty)
            return Fail("You cannot manage sample work schedules.");
        var error = SampleWorkScheduleRules.Validate(command);
        if (error is not null) return Fail(error);
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.EmployeeId == actor && x.IsActive, ct))
            return Fail("Current employee is not active in this company.");
        var scope = await visibility.BuildScopeAsync(ct);
        scope = await SampleWorkPlanningVisibility.ForPlanningAsync(scope, user, db.Employees, ct);
        var requests = visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), scope)
            .Where(x => x.CompanyId == company && x.SampleRequestId == command.SampleRequestId && x.IsActive &&
                x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "LabDeclined" &&
                x.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId && x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);
        var sample = await requests.SingleOrDefaultAsync(ct);
        if (sample is null) return Fail("Sample request is outside your scope or has ended.");
        if (await Services.Workflow.SampleRequestLabPauseRules.IsPausedAsync(db, company, sample.SampleRequestId, ct))
            return Fail(Services.Workflow.SampleRequestLabPauseRules.PausedMessage);
        if (sample.UpdatedDate != command.ExpectedUpdatedDate) return Fail("Sample request changed. Reload before saving.");
        var delivery = SampleWorkScheduleRules.Resolve(sample.ExpectedDeliveryDate, command.ExpectedDeliveryDate,
            SampleWorkScheduleRules.DeliveryDate, command.ClearFields);
        var quote = SampleWorkScheduleRules.Resolve(sample.ExpectedPriceQuoteDate, command.ExpectedPriceQuoteDate,
            SampleWorkScheduleRules.QuoteDate, command.ClearFields);
        if (delivery == sample.ExpectedDeliveryDate && quote == sample.ExpectedPriceQuoteDate)
            return OperationResult<Guid>.Ok(sample.SampleRequestId, "No schedule dates changed.");

        var oldDelivery = sample.ExpectedDeliveryDate;
        var oldQuote = sample.ExpectedPriceQuoteDate;
        var now = clock.Now;
        await using var transaction = await db.BeginTransactionAsync(ct);
        // The visibility, open status and version token are checked again in the atomic write.
        var changed = await requests.Where(x => x.UpdatedDate == command.ExpectedUpdatedDate)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.ExpectedDeliveryDate, delivery)
                .SetProperty(x => x.ExpectedPriceQuoteDate, quote).SetProperty(x => x.UpdatedDate, now)
                .SetProperty(x => x.UpdatedBy, actor), ct);
        if (changed != 1) return Fail("Sample request changed. Reload before saving.");
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();
        if (oldDelivery != delivery)
        {
            oldValues[nameof(sample.ExpectedDeliveryDate)] = oldDelivery;
            newValues[nameof(sample.ExpectedDeliveryDate)] = delivery;
        }
        if (oldQuote != quote)
        {
            oldValues[nameof(sample.ExpectedPriceQuoteDate)] = oldQuote;
            newValues[nameof(sample.ExpectedPriceQuoteDate)] = quote;
        }
        db.AuditLogs.Add(new AuditLog
        {
            CompanyId = company, SchemaName = "SampleRequests", TableName = "SampleRequests", RecordId = sample.SampleRequestId,
            ActionType = AuditActionType.Update, ChangedBy = actor, ChangedAt = now, Reason = SampleWorkScheduleRules.AuditReason,
            CorrelationId = sample.SampleRequestId, OldValues = JsonSerializer.SerializeToDocument(oldValues),
            NewValues = JsonSerializer.SerializeToDocument(newValues)
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult<Guid>.Ok(sample.SampleRequestId);
    }

    private static OperationResult<Guid> Fail(string message) => OperationResult<Guid>.Fail(message);
}
