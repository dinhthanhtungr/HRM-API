using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

/// <summary>Restores the pre-Trial delivery behavior from commit 450239c behind the shared status API.</summary>
internal sealed class LegacySampleDeliveryService(IPLMWriteDbContext db, FormulaVersionService versions)
{
    public async Task<OperationResult<FormulaWriteResultDto>> SendAsync(SampleRequest request, Formula formula,
        Guid actor, DateTime now, CancellationToken ct)
    {
        if (!SampleFlowRules.IsLegacy(request.CreatedDate) || !SampleFlowRules.IsOpen(request.Status) || formula.Status != "Approved")
            return OperationResult<FormulaWriteResultDto>.Fail("This legacy request cannot send this formula.");
        await using var transaction = await db.BeginTransactionAsync(ct);
        var oldStatus = request.Status;
        formula.Status = FormulaStatus.SampleSent.ToString();
        formula.SentBy = actor;
        formula.SentDate = now;
        formula.UpdatedBy = actor;
        formula.UpdatedDate = now;
        request.Status = SampleRequestStatus.SampleSent.ToString();
        request.FormulaId = formula.FormulaId;
        request.SendBy = actor;
        request.SendDate = now;
        request.UpdatedBy = actor;
        request.UpdatedDate = now;
        await DataChangeRequests.SampleRequestDataChangeAuditHelper.AddStatusTransitionAuditIfChangedAsync(
            db.AuditLogs, request, oldStatus, actor, now, "LegacyFormulaSampleSent", ct);
        var evt = SampleRequestWorkflowEvents.Create(request.SampleRequestId, request.CompanyId, actor, now,
            SampleRequestWorkflowEvents.SampleSent, formulaId: formula.FormulaId);
        db.SampleRequestWorkflowEvents.Add(evt);
        SampleRequestWorkflowNotificationQueue.Queue(db, evt, new SendSampleRequestMessageCommand
        {
            SampleRequestId = request.SampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = $"Lab đã gửi mẫu cho yêu cầu {request.ExternalId}. Công thức: {formula.ExternalId}.",
            TopicOverride = TopicNotifications.SampleRequestSampleSent,
            TitleOverride = "Lab đã gửi mẫu"
        });
        await versions.SaveSnapshotAsync(formula, actor, now, "Legacy sample sent", true, ct);
        await transaction.CommitAsync(ct);
        return OperationResult<FormulaWriteResultDto>.Ok(FormulaWriteService.ToResult(formula, 1));
    }
}
