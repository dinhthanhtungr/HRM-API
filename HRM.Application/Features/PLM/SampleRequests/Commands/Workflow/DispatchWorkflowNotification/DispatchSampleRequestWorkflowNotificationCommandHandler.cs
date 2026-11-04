using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.DispatchWorkflowNotification;

internal sealed class DispatchSampleRequestWorkflowNotificationCommandHandler(IPLMWriteDbContext db, ISender sender, FormulaPricingReviewService pricingReview)
    : IRequestHandler<DispatchSampleRequestWorkflowNotificationCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(DispatchSampleRequestWorkflowNotificationCommand request, CancellationToken ct)
    {
        var job = await db.OutboxMessages.SingleOrDefaultAsync(x => x.Id == request.OutboxId &&
            x.Type == SampleRequestWorkflowNotificationQueue.OutboxType && x.ProcessedAt == null, ct);
        if (job is null) return Fail(WorkflowNotificationFailureCode.JobUnavailable);
        var payload = JsonSerializer.Deserialize<WorkflowNotificationPayload>(job.PayloadJson);
        if (payload is null) return Fail(WorkflowNotificationFailureCode.InvalidPayload);
        var evt = await db.SampleRequestWorkflowEvents.SingleOrDefaultAsync(x => x.Id == payload.EventId &&
            x.CompanyId == payload.CompanyId, ct);
        if (evt is null || evt.RecordedByEmployeeId is not { } actor ||
            !await db.Employees.AnyAsync(x => x.EmployeeId == actor && x.CompanyId == evt.CompanyId && x.IsActive, ct))
            return Fail(WorkflowNotificationFailureCode.EventActorUnavailable);
        if (evt.NotificationId.HasValue) return OperationResult<Guid>.Ok(evt.Id);
        var source = await db.SampleRequests.AsNoTracking()
            .Where(x => x.SampleRequestId == evt.SampleRequestId && x.CompanyId == evt.CompanyId)
            .Select(x => new { x.CreatedDate }).SingleOrDefaultAsync(ct);
        // Successful no-op also consumes previously queued tracking notices for legacy records.
        if (source is not null && !SampleRequestWorkflowPublicationRules.ShouldDispatch(source.CreatedDate, evt.EventType))
            return OperationResult<Guid>.Ok(evt.Id);

        // Never repoint an old General message to a new pricing copy: that would sever its visibility link.
        // Keep persisted history untouched and require an explicit operational review instead.
        if (SampleRequestPriceFeedbackVisibility.IsPriceEvent(evt.EventType) && evt.MessageId is { } historicalMessageId &&
            !await db.InternalMessages.AsNoTracking().AnyAsync(m => m.InternalMessageId == historicalMessageId &&
                m.Conversation.CompanyId == evt.CompanyId && m.Conversation.IsActive &&
                m.Conversation.RelatedType == InternalMailRelatedType.ConversationPricing &&
                db.InternalConversations.Any(root => root.InternalConversationId == m.Conversation.RelatedId &&
                    root.CompanyId == evt.CompanyId && root.IsActive &&
                    root.RelatedType == InternalMailRelatedType.SampleRequest && root.RelatedId == evt.SampleRequestId), ct))
            return Fail(WorkflowNotificationFailureCode.HistoricalPricePublicationRequiresReview);

        var title = evt.EventType switch
        {
            "LabFormulaConfirmed" => "Lab đã xác nhận công thức",
            "LabStandardReceived" => "Lab đã nhận mẫu chuẩn",
            "CustomerPriceAccepted" => "Khách đã đồng ý giá",
            "CustomerPriceAcceptanceRevoked" => "Đã thu hồi ghi nhận đồng ý giá",
            "SampleSent" => "Lab đã gửi mẫu",
            "CustomerFeedbackRecorded" => "Đã ghi nhận phản hồi khách",
            SampleFlowRules.PriceFeedbackShared => "Đã ghi nhận phản hồi của khách",
            SampleFlowRules.QuotationFinalized => "Lab đã hoàn thành công thức báo giá",
            SampleFlowRules.ChipFinalized => "Lab đã chốt công thức theo mẫu khách chọn",
            SampleFlowRules.ChipReopened => "Lab đã mở lại công thức chip",
            SampleFlowRules.KgRequested => "Sale yêu cầu chạy mẫu theo kg",
            SampleFlowRules.CompletedWithoutTrial => "Sale đã hoàn thành yêu cầu",
            SampleFlowRules.PriceRejected => "Khách chưa đồng ý giá",
            SampleFlowRules.PriceNoResponse => "Chưa có phản hồi giá của khách",
            _ => "Cập nhật yêu cầu phối mẫu"
        };
        var sharedFeedback = evt.EventType == SampleFlowRules.PriceFeedbackShared;
        var topic = sharedFeedback ? TopicNotifications.SampleRequestCustomerFeedbackRecorded : payload.Topic ?? (evt.EventType == "CustomerFeedbackRecorded"
            ? TopicNotifications.SampleRequestCustomerFeedbackRecorded
            : TopicNotifications.SampleRequestMessageCreated);
        var trial = evt.SampleRequestSampleTrialId is { } trialId
            ? await db.SampleRequestSampleTrials.AsNoTracking().Where(x => x.SampleRequestSampleTrialId == trialId &&
                x.SampleRequestId == evt.SampleRequestId).Select(x => (int?)x.TrialNo).SingleOrDefaultAsync(ct) : null;
        var priceEvent = SampleRequestPriceFeedbackVisibility.IsPriceEvent(evt.EventType);
        // Private notes/reference stay in the pricing group; explicit companion notes alone go to General.
        var body = priceEvent ? $"{title}. {evt.Note}{(string.IsNullOrWhiteSpace(evt.PriceReference) ? "" : $" · Căn cứ: {evt.PriceReference}")}"
            : sharedFeedback ? $"{title}. {evt.Note}"
            : payload.Message ?? $"{title}{(trial.HasValue ? $" · Lần thử {trial}" : "")}. {evt.Note}";
        if (body.Length > 2000) body = body[..1999];
        OperationResult<SendInternalMessageResultDto> result;
        if (evt.EventType == "LabFormulaConfirmed" && evt.FormulaId is { } confirmedFormulaId)
        {
            var formula = await db.Formulas.AsNoTracking().SingleOrDefaultAsync(x => x.FormulaId == confirmedFormulaId &&
                x.CompanyId == evt.CompanyId && x.IsActive, ct);
            if (formula is null) return Fail(WorkflowNotificationFailureCode.FormulaUnavailable);
            result = await pricingReview.SendAsync(evt.SampleRequestId, formula.ProductId, evt.CompanyId,
                formula.FormulaId, formula.ExternalId, formula.Name, payload.MaterialsChanged, ct, evt.Id);
        }
        else result = await sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = evt.SampleRequestId, WorkflowEventId = evt.Id,
            Type = payload.Type, Message = body, TopicOverride = topic,
            TitleOverride = priceEvent || sharedFeedback ? title : payload.Title ?? title, IsInitialMessage = payload.IsInitialMessage,
            NotificationRecipientEmployeeIdsOverride = payload.Recipients,
            ExtraRecipientEmployeeIds = payload.ExtraRecipients ?? [],
            SilentWatcherEmployeeIds = payload.SilentWatchers,
            UseDefaultSilentWatchersWhenOmitted = payload.UseDefaultSilentWatchers
        }, ct);
        if (!result.Success || result.Data is null)
            return Fail(WorkflowNotificationFailureRules.FromMessage(result.Message));
        if (result.Data.MessageId != Guid.Empty)
        {
            evt.MessageId = result.Data.MessageId;
            evt.ConversationId = result.Data.ConversationId;
            evt.NotificationId = result.Data.NotificationId == Guid.Empty ? null : result.Data.NotificationId;
        }
        await db.SaveChangesAsync(ct);
        return OperationResult<Guid>.Ok(evt.Id);
    }

    private static OperationResult<Guid> Fail(WorkflowNotificationFailureCode code)
        => OperationResult<Guid>.Fail(code.ToString());
}
