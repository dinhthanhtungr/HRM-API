using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;

/// <summary>One parent lock and transaction; each option uses the existing feedback use case.</summary>
internal sealed class RecordSampleBatchFeedbackCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility, ISender sender, IDateTimeProvider clock,
    SampleDeliveryCompletion completion)
    : IRequestHandler<RecordSampleBatchFeedbackCommand, OperationResult<Guid>>
{
    internal const string EventType = "SampleBatchFeedbackSaved";

    public async Task<OperationResult<Guid>> Handle(RecordSampleBatchFeedbackCommand command, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is not { } actor ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow) || command.OperationId == Guid.Empty ||
            command.Options is null || command.Options.Count is < 1 or > 30 ||
            command.Options.Any(o => o is null || o.TrialId == Guid.Empty || o.OperationId == Guid.Empty ||
                o.OperationId == command.OperationId || o.Note?.Length > 5000 || o.CorrectionReason?.Length > 2000) ||
            command.Options.Select(o => o.TrialId).Distinct().Count() != command.Options.Count ||
            command.Options.Select(o => o.OperationId).Distinct().Count() != command.Options.Count)
            return Fail("Batch feedback input is invalid.");
        var scope = await visibility.BuildScopeAsync(ct);
        var parent = await visibility.ApplySampleRequestVisibility(db.SampleRequests, db.Customers.AsNoTracking(), scope)
            .AsTracking().SingleOrDefaultAsync(x => x.SampleRequestId == command.SampleRequestId && x.CompanyId == company && x.IsActive, ct);
        if (parent is null || !SampleSaleWorkflowAccess.CanActFor(user, permissions, parent.ManagerBy, scope) ||
            !await db.Employees.AsNoTracking().AnyAsync(e => e.CompanyId == company && e.EmployeeId == actor && e.IsActive, ct))
            return Fail("Sample request is outside your Sale workflow scope.");
        var content = JsonSerializer.Serialize(command.Options.OrderBy(o => o.TrialId).Select(o => new {
            o.OperationId, o.TrialId, o.Approved, o.ReplyDate, Note = o.Note?.Trim(), Reason = o.CorrectionReason?.Trim() }));
        if (command.PreserveSiblingResults) content = "explicit:" + content;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        var replay = await db.SampleRequestWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Id == command.OperationId, ct);
        if (replay is not null)
            return replay.CompanyId == company && replay.SampleRequestId == parent.SampleRequestId && replay.RecordedByEmployeeId == actor &&
                replay.EventType == EventType && replay.RelatedEventId == command.BatchId && replay.Note == hash
                ? OperationResult<Guid>.Ok(command.OperationId) : Fail("Operation id is already used with different feedback.");
        if (!command.Options.Any(option => !string.IsNullOrWhiteSpace(option.Note)))
            return Fail("At least one sample feedback note is required.");
        if (!SampleFlowRules.IsOpen(parent.Status)) return Fail("The request is closed.");
        await using var transaction = await db.BeginTransactionAsync(ct);
        var locked = await db.SampleRequests.Where(x => x.SampleRequestId == parent.SampleRequestId && x.CompanyId == company &&
            x.IsActive && x.UpdatedDate == command.ExpectedRequestUpdatedDate)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, x => x.UpdatedDate), ct);
        if (locked != 1) return Fail("Request changed. Reload before saving.");
        if (await SampleRequestLabPauseRules.IsPausedAsync(db, company, parent.SampleRequestId, ct))
            return Fail(SampleRequestLabPauseRules.PausedMessage);
        var ids = command.Options.Select(o => o.TrialId).ToArray();
        var originals = await db.SampleRequestSampleTrials.AsNoTracking().Where(t => t.SampleRequestId == parent.SampleRequestId &&
            t.IsActive && ids.Contains(t.SampleRequestSampleTrialId)).ToDictionaryAsync(t => t.SampleRequestSampleTrialId, ct);
        if (originals.Count != ids.Length || command.Options.Any(o => originals[o.TrialId].UpdatedDate != o.ExpectedTrialUpdatedDate ||
            o.ExpectedRequestUpdatedDate != command.ExpectedRequestUpdatedDate))
            return Fail("A sample changed. Reload before saving the batch.");
        if (command.Options.Count(o => o.Approved) > 1)
            return Fail("Choose one accepted sample per delivery batch.");
        // First acceptance applies the established automatic sibling outcomes, then explicit row feedback.
        var ordered = command.Options.OrderByDescending(o => o.Approved && !originals[o.TrialId].CustomerReplyDate.HasValue)
            .ThenByDescending(o => o.Approved).ThenBy(o => o.TrialId).ToArray();
        foreach (var input in ordered)
        {
            if (await db.SampleRequestWorkflowEvents.AsNoTracking().AnyAsync(e => e.Id == input.OperationId, ct))
                return Fail("A feedback operation id is already used. Reload before saving.");
            var current = await db.SampleRequestSampleTrials.SingleAsync(t => t.SampleRequestSampleTrialId == input.TrialId &&
                t.SampleRequestId == parent.SampleRequestId && t.IsActive, ct);
            var result = await sender.Send(new RecordSampleOptionFeedbackCommand {
                SampleRequestId = parent.SampleRequestId, BatchId = command.BatchId, OperationId = input.OperationId,
                TrialId = input.TrialId, Approved = input.Approved, ReplyDate = input.ReplyDate, Note = input.Note,
                CorrectionReason = input.CorrectionReason, ExpectedRequestUpdatedDate = parent.UpdatedDate,
                ExpectedTrialUpdatedDate = current.UpdatedDate, UseExistingTransaction = true,
                PreserveSiblingResults = !input.Approved,
                SuppressNotification = true,
                DeferCompletion = true,
                ImplicitFeedbackWithinBatch = !originals[input.TrialId].CustomerReplyDate.HasValue && current.CustomerReplyDate.HasValue
            }, ct);
            if (!result.Success) return Fail(result.Message ?? "Batch feedback could not be saved.");
        }
        var winner = await db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == parent.SampleRequestId &&
            t.IsActive && t.Status == SampleTrialStatus.Approved &&
            db.SampleRequestWorkflowEvents.Any(e => e.CompanyId == company && e.SampleRequestId == parent.SampleRequestId &&
                e.EventType == SampleRequestWorkflowEvents.SampleSent && e.RelatedEventId == command.BatchId &&
                e.SampleRequestSampleTrialId == t.SampleRequestSampleTrialId))
            .OrderByDescending(t => t.TrialNo).FirstOrDefaultAsync(ct);
        if (winner is not null && !await completion.ApplyAsync(parent, winner, actor, clock.Now, ct))
            return Fail("Formula changed. Reload before saving feedback.");
        var marker = SampleRequestWorkflowEvents.Create(parent.SampleRequestId, company, actor, clock.Now, EventType, note: hash);
        marker.Id = command.OperationId;
        marker.RelatedEventId = command.BatchId;
        db.SampleRequestWorkflowEvents.Add(marker);
        var formulaIds = originals.Values.Where(trial => trial.FormulaId.HasValue).Select(trial => trial.FormulaId!.Value).Distinct().ToArray();
        var codes = await db.Formulas.AsNoTracking().Where(formula => formula.CompanyId == company &&
            formula.ProductId == parent.ProductId && formulaIds.Contains(formula.FormulaId))
            .ToDictionaryAsync(formula => formula.FormulaId, formula => formula.ExternalId, ct);
        var sentAt = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(evt => evt.Id == command.BatchId &&
            evt.CompanyId == company && evt.SampleRequestId == parent.SampleRequestId)
            .Select(evt => evt.OccurredAt ?? evt.RecordedAt).SingleAsync(ct);
        var message = SampleBatchFeedbackMessage.Build(parent.ExternalId ?? "", sentAt,
            command.Options.OrderBy(option => originals[option.TrialId].TrialNo).Select(option => (
                originals[option.TrialId].FormulaId is { } formulaId ? codes.GetValueOrDefault(formulaId) : null,
                option.Approved, option.Note)));
        if (message is not null) SampleRequestWorkflowNotificationQueue.Queue(db, marker, new SendSampleRequestMessageCommand {
            SampleRequestId = parent.SampleRequestId, Type = SampleRequestNotificationType.GeneralMessage,
            TopicOverride = TopicNotifications.SampleRequestCustomerFeedbackRecorded,
            TitleOverride = $"Phản hồi mẫu theo đợt · {parent.ExternalId}", Message = message
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult<Guid>.Ok(command.OperationId);
    }
    private static OperationResult<Guid> Fail(string message) => OperationResult<Guid>.Fail(message);
}
