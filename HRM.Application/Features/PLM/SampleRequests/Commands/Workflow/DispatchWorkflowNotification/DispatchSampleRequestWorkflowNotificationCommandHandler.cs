using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
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

internal sealed class DispatchSampleRequestWorkflowNotificationCommandHandler(IPLMWriteDbContext db, ISender sender, FormulaPricingReviewService pricingReview, ICRMReadDbContext crm)
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
        // Consume old queued outcome-only notices without creating a redundant pricing message.
        if (!SampleRequestWorkflowPublicationRules.ShouldPublishMessage(evt))
            return OperationResult<Guid>.Ok(evt.Id);
        var source = await db.SampleRequests.AsNoTracking()
            .Where(x => x.SampleRequestId == evt.SampleRequestId && x.CompanyId == evt.CompanyId)
            .SingleOrDefaultAsync(ct);
        // Successful no-op also consumes previously queued tracking notices for legacy records.
        if (source is not null && !SampleRequestWorkflowPublicationRules.ShouldDispatch(source.CreatedDate, evt.EventType) &&
            !await db.SampleRequestWorkflowEvents.AnyAsync(e => e.CompanyId == evt.CompanyId && e.SampleRequestId == evt.SampleRequestId &&
                e.EventType == SampleFollowUpRules.Started && e.RecordedAt <= evt.RecordedAt, ct))
            return OperationResult<Guid>.Ok(evt.Id);

        // Never repoint an old General message to a new pricing copy: that would sever its visibility link.
        // Keep persisted history untouched and require an explicit operational review instead.
        if (SampleRequestPriceFeedbackVisibility.IsPrivatePublication(evt.EventType) && evt.MessageId is { } historicalMessageId &&
            !await db.InternalMessages.AsNoTracking().AnyAsync(m => m.InternalMessageId == historicalMessageId &&
                m.Conversation.CompanyId == evt.CompanyId && m.Conversation.IsActive &&
                m.Conversation.RelatedType == InternalMailRelatedType.ConversationPricing &&
                db.InternalConversations.Any(root => root.InternalConversationId == m.Conversation.RelatedId &&
                    root.CompanyId == evt.CompanyId && root.IsActive &&
                    root.RelatedType == InternalMailRelatedType.SampleRequest && root.RelatedId == evt.SampleRequestId), ct))
            return Fail(WorkflowNotificationFailureCode.HistoricalPricePublicationRequiresReview);

        var title = evt.EventType switch
        {
            SampleFlowRules.StandardPriceApproved => "BGD đã duyệt giá chuẩn",
            SampleRequestWorkflowEvents.LabNote => "Lưu ý từ Lab gửi Sale",
            "LabFormulaConfirmed" => "Lab đã xác nhận công thức",
            "LabStandardReceived" => "Lab đã nhận mẫu chuẩn",
            "CustomerPriceAccepted" => "Khách đã đồng ý giá",
            "CustomerPriceAcceptanceRevoked" => "Đã thu hồi ghi nhận đồng ý giá",
            "SampleSent" => "Lab đã gửi mẫu",
            "CustomerFeedbackRecorded" => "Đã ghi nhận phản hồi khách",
            DeliveryOptions.RecordSampleBatchFeedbackCommandHandler.EventType => "Đã ghi nhận phản hồi mẫu theo đợt",
            SampleFlowRules.PriceFeedbackShared => "Đã ghi nhận phản hồi của khách",
            SampleFlowRules.QuotationFinalized => "Lab đã hoàn thành công thức báo giá",
            SampleFlowRules.ChipFinalized => "Lab đã chốt công thức theo mẫu khách chọn",
            SampleFlowRules.ChipReopened => "Lab đã mở lại công thức chip",
            SampleFlowRules.KgRequested => "Sale yêu cầu chạy mẫu theo kg",
            SampleFlowRules.ChipRequested => "Sale yêu cầu gửi chip",
            SampleFlowRules.ChipThenSampleRequested => "Sale yêu cầu chip và mẫu kg",
            SampleRequestWorkflowEvents.LabDeclined => "Lab từ chối yêu cầu",
            SampleRequestLabPauseRules.Rejected => "Lab từ chối — tạm dừng yêu cầu",
            SampleRequestLabPauseRules.Resumed => "Sale mở lại yêu cầu",
            SampleFlowRules.LabFinalizationRequested => "Sale nhắn Lab chốt công thức để lên đơn",
            SampleFlowRules.ChipFinalizationRequested => "Sale nhờ Lab chốt công thức chip để lên đơn",
            SampleFlowRules.ChipFollowUpDeferred => "Sale tiếp tục theo dõi khách sau khi duyệt chip",
            SampleFlowRules.CompletionDeferred => "Sale thu lại yêu cầu hoàn thành và tiếp tục theo dõi khách",
            SampleFlowRules.LowerPriceRequested => "Sale yêu cầu Lab phát triển phương án giá thấp hơn",
            SampleFlowRules.PriceFollowUpDeferred => "Sale tiếp tục chờ khách phản hồi giá",
            SampleFlowRules.CompletedWithoutTrial => "Sale đã hoàn thành yêu cầu",
            SampleFlowRules.PriceRejected => "Khách chưa đồng ý giá",
            SampleFlowRules.PriceNoResponse => "Chưa có phản hồi giá của khách",
            _ => "Cập nhật yêu cầu phối mẫu"
        };
        var sharedFeedback = evt.EventType == SampleFlowRules.PriceFeedbackShared;
        var topic = evt.EventType == SampleRequestWorkflowEvents.LabNote ? TopicNotifications.SampleRequestLabNoteRecorded : sharedFeedback ? TopicNotifications.SampleRequestCustomerFeedbackRecorded : payload.Topic ?? (evt.EventType == "CustomerFeedbackRecorded"
            ? TopicNotifications.SampleRequestCustomerFeedbackRecorded
            : TopicNotifications.SampleRequestMessageCreated);
        var trial = evt.SampleRequestSampleTrialId is { } trialId
            ? await db.SampleRequestSampleTrials.AsNoTracking().Where(x => x.SampleRequestSampleTrialId == trialId &&
                x.SampleRequestId == evt.SampleRequestId).Select(x => (int?)x.TrialNo).SingleOrDefaultAsync(ct) : null;
        var priceEvent = SampleRequestPriceFeedbackVisibility.IsPrivatePublication(evt.EventType);
        // Private notes/reference stay in the pricing group; explicit companion notes alone go to General.
        var parent = sharedFeedback && evt.RelatedEventId.HasValue
            ? await db.SampleRequestWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == evt.RelatedEventId &&
                x.CompanyId == evt.CompanyId && x.SampleRequestId == evt.SampleRequestId, ct) : null;
        var formulaId = evt.FormulaId ?? parent?.FormulaId;
        var productId = source?.ProductId;
        var formulaCode = formulaId.HasValue ? await db.Formulas.AsNoTracking().Where(x => x.FormulaId == formulaId &&
            x.CompanyId == evt.CompanyId && (!productId.HasValue || x.ProductId == productId))
            .Select(x => x.ExternalId).SingleOrDefaultAsync(ct) : null;
        string? next = null;
        if (!payload.FeedbackNoteUpdated && source?.IsActive == true && SampleFlowRules.IsOpen(source.Status) &&
            !sharedFeedback && !priceEvent &&
            evt.EventType is SampleFlowRules.QuotationFinalized or SampleFlowRules.ChipFinalized)
        {
            var flow = await SampleFlowStateReader.ReadAsync(db, crm, source, false, false, ct);
            next = WorkflowMessageText.Next(flow.Stage);
        }
        if (evt.EventType == SampleFlowRules.CompletedWithoutTrial && !string.IsNullOrWhiteSpace(source?.ExternalId))
            title = "Yêu cầu đã hoàn thành · " + source.ExternalId;
        var text = WorkflowMessageText.Build(evt, parent, title, formulaCode, next, payload.FeedbackNoteUpdated, payload.Message, trial);
        title = text.Title;
        var body = text.Body;
        if (evt.EventType == SampleFlowRules.StandardPriceApproved)
        {
            var approver = await db.Employees.AsNoTracking().Where(e => e.EmployeeId == actor && e.CompanyId == evt.CompanyId)
                .Select(e => e.FullName).SingleOrDefaultAsync(ct);
            body = $"{payload.Message}\nCông thức: {formulaCode}\nNgười duyệt: {approver}";
        }
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
