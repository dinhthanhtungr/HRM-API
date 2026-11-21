using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.DispatchWorkflowNotification;

/// <summary>Plain-text message content; General never incorporates a parent pricing note or reference.</summary>
internal static class WorkflowMessageText
{
    internal static string? Outcome(string? type) => type switch
    {
        "CustomerPriceAccepted" or "CustomerPriceConfirmed" => "Khách đồng ý giá",
        "CustomerPriceRejected" => "Khách không đồng ý giá",
        "CustomerPriceNoResponse" => "Khách chưa phản hồi giá",
        "CustomerPriceAcceptanceRevoked" or "CustomerPriceConfirmationRevoked" => "Đã thu hồi ghi nhận đồng ý giá",
        _ => null
    };

    internal static string? Next(string? stage) => stage switch
    {
        "AwaitingPriceDecision" => "Sale chọn yêu cầu phương án giá thấp hơn, chờ khách hoặc hủy TP.",
        "PriceFollowUp" => "Sale tiếp tục chờ và theo dõi phản hồi của khách.",
        "PriceRework" => "Lab điều chỉnh công thức; lưu thay đổi để gửi BGD duyệt giá lại.",
        "FinalizeQuotationFormula" => "Lab chốt công thức báo giá.",
        "QuotationAccepted" => "Sale xác nhận hoàn thành hồ sơ.",
        "AwaitingSaleDecision" => "Sale hoàn thành hồ sơ hoặc yêu cầu chạy mẫu.",
        "AwaitingSaleAfterChip" => "Sale chọn chạy mẫu, nhờ Lab chốt để lên đơn, tiếp tục theo dõi hoặc hủy yêu cầu phối mẫu.",
        "ChipFollowUp" => "Sale tiếp tục theo dõi khách sau khi duyệt chip.",
        "RequestFollowUp" => "Sale tiếp tục theo dõi khách; Lab chưa cần chốt công thức.",
        "FinalizeChipFormula" => "Lab chốt công thức chip theo phương án khách đã chọn.",
        "AwaitingPriceFeedback" => "Sale theo dõi phản hồi giá của khách.",
        "AwaitingStandardPrice" => "BGD duyệt giá chuẩn.",
        "ReadyToSendSample" => "Lab chuẩn bị và gửi mẫu.",
        "AwaitingChipFeedback" => "Sale lấy phản hồi thẻ màu từ khách.",
        "AwaitingSampleFeedback" => "Sale lấy kết quả thử mẫu từ khách.",
        "ReworkChip" => "Lab xem phản hồi và làm lại công thức.",
        _ => null // Ambiguous or ended stages must not invent a next task.
    };

    internal static (string Title, string Body) Build(SampleRequestWorkflowEvent evt, SampleRequestWorkflowEvent? parent,
        string defaultTitle, string? formulaCode, string? next, bool noteUpdated, string? fallbackMessage, int? trialNo)
    {
        var shared = evt.EventType == SampleFlowRules.PriceFeedbackShared;
        if (evt.EventType == DeliveryOptions.RecordSampleBatchFeedbackCommandHandler.EventType)
            return (defaultTitle, fallbackMessage ?? defaultTitle); // Never publish the retry hash stored in Note.
        var price = SampleRequestPriceFeedbackVisibility.IsPriceEvent(evt.EventType);
        var title = shared ? noteUpdated ? "Sale cập nhật nội dung phản hồi khách" : Outcome(parent?.EventType) ?? defaultTitle
            : price ? Outcome(evt.EventType) ?? defaultTitle : defaultTitle;
        var structured = price || shared || evt.EventType is SampleFlowRules.QuotationFinalized or
            SampleFlowRules.CompletedWithoutTrial or SampleFlowRules.ChipFinalized or SampleFlowRules.ChipReopened or SampleFlowRules.KgRequested or
            SampleFlowRules.ChipThenSampleRequested or SampleFlowRules.ChipRequested or SampleFlowRules.CompletionDeferred;
        if (!structured) return (title, fallbackMessage ?? $"{title}{(trialNo.HasValue ? $" · Lần thử {trialNo}" : "")}. {evt.Note}");
        var lines = new List<string> { title };
        if (noteUpdated && shared && Outcome(parent?.EventType) is { } outcome) lines.Add("Kết quả hiện tại: " + outcome);
        if (!string.IsNullOrWhiteSpace(evt.Note))
            lines.Add((price ? "Trao đổi giá: " : shared ? "Nội dung gửi Lab: " : "Ghi chú: ") + evt.Note.Trim());
        if (!string.IsNullOrWhiteSpace(formulaCode)) lines.Add("Công thức: " + formulaCode);
        if (price && !string.IsNullOrWhiteSpace(evt.PriceReference)) lines.Add("Báo giá: " + evt.PriceReference.Trim());
        if (!price && !shared && !noteUpdated && evt.EventType != SampleFlowRules.QuotationFinalized &&
            !string.IsNullOrWhiteSpace(next)) lines.Add("Tiếp theo: " + next);
        return (title, string.Join("\n", lines));
    }
}
