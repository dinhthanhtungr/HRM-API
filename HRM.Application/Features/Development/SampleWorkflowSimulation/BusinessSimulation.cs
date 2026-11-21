using System.Globalization;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using static HRM.Application.Features.Development.SampleWorkflowSimulation.SimulationCatalog;

namespace HRM.Application.Features.Development.SampleWorkflowSimulation;

/// <summary>In-memory evidence adapter. Assignments and choices come from the live PLM reader.</summary>
internal static class BusinessSimulation
{
    private const string LabRejected = SampleRequestLabPauseRules.Rejected;
    private const string SaleReopened = SampleRequestLabPauseRules.Resumed;
    private static readonly DateTime Epoch = new(2026, 11, 2);
    private static readonly Guid RequestId = new("00000000-0000-0000-0000-000000000001");
    private static Guid FormulaId(int number) => new(number + 10, 0, 0, new byte[8]);
    private static Guid TrialId(int number) => new(number + 100, 0, 0, new byte[8]);
    private static readonly IReadOnlySet<string> KnownEvents = new HashSet<string> {
        LabRejected, SaleReopened, SampleRequestWorkflowEvents.LabDeclined,
        "SimulationLabFormulaRejected", "SimulationSaleReopenedFormula",
        SampleRequestWorkflowEvents.FormulaConfirmed, SampleRequestWorkflowEvents.SampleSent, SampleRequestWorkflowEvents.FeedbackRecorded,
        SampleRequestWorkflowEvents.PriceAccepted, SampleRequestWorkflowEvents.PriceRevoked, SampleRequestWorkflowEvents.RequestCancelled,
        SampleFlowRules.StandardPriceApproved, SampleFlowRules.QuotationRequired, SampleFlowRules.PriceNoResponse, SampleFlowRules.PriceRejected,
        SampleFlowRules.ChipRequested, SampleFlowRules.KgRequested, SampleFlowRules.ChipThenSampleRequested, SampleFlowRules.LabFinalizationRequested,
        SampleFlowRules.ChipFinalizationRequested, SampleFlowRules.ChipFinalized, SampleFlowRules.QuotationFinalized, SampleFlowRules.ChipReopened,
        SampleFlowRules.CompletionDeferred, SampleFlowRules.ChipFollowUpDeferred, SampleFlowRules.PriceFollowUpDeferred, SampleFlowRules.LowerPriceRequested,
        SampleFlowRules.CompletedWithoutTrial };

    internal static SimulationState Sync(SimulationState state)
    {
        var w = state.Workflow!;
        var outcome = w.Events.LastOrDefault(e => e is SampleRequestWorkflowEvents.PriceAccepted or SampleFlowRules.PriceRejected or SampleFlowRules.PriceNoResponse or SampleRequestWorkflowEvents.PriceRevoked);
        var chip = w.Plan is "Chip" or "ChipThenSample";
        var sample = w.Plan is "Sample" or "ChipThenSample";
        return state with {
            FormulaReady = w.FormulaStatus is "Approved" or "SampleSent" or "Completed",
            PriceAccepted = outcome == SampleRequestWorkflowEvents.PriceAccepted, PriceRejected = outcome == SampleFlowRules.PriceRejected,
            PricePending = w.StandardPriceApproved && outcome != SampleRequestWorkflowEvents.PriceAccepted,
            QuotePending = false, QuoteReady = w.StandardPriceApproved && w.QuotedAt is null,
            ChipRequired = chip, SampleRequired = sample, Combined = w.Plan == "ChipThenSample",
            ChipSets = chip && w.TrialKind == "" ? w.RequestedSets : 0, SampleKg = sample && w.TrialKind != "Sample" ? w.RequestedKg : 0,
            ChipSent = w.TrialKind == "Chip" || w.Plan == "ChipThenSample" && w.TrialKind == "Sample",
            ChipResult = w.TrialKind == "Chip" ? w.TrialResult : "", ChipFeedbackDone = w.TrialKind == "Chip" && w.TrialResult.Length > 0,
            HasHistory = w.TrialNo > 0,
            Batches = w.TrialKind == "Sample" ? [new(w.TrialNo, w.SentDate, [new("option-1-1", "VU_SIM_001", w.DeliveredKg, w.TrialResult,
                state.Note, w.TrialResult.Length > 0 ? w.ReplyDate ?? "2026-11-15" : null)])] : []
        };
    }

    private static (SampleRequest Request, Formula Formula, SampleRequestSampleTrial? Trial, SampleRequestSampleTrial[] Trials, SampleRequestWorkflowEvent[] Events, DateTime? PriceAt) Evidence(SimulationState s)
    {
        var w = s.Workflow!;
        var formulaId = FormulaId(w.FormulaStarts.Count);
        var formula = new Formula { FormulaId = formulaId, Status = w.FormulaStatus, CreatedDate = Epoch };
        var request = new SampleRequest { SampleRequestId = RequestId, CreatedDate = Epoch, Status = s.Status is "Open" or "Rejected" ? "InProgress" : s.Status,
            InfoType = w.Plan is "Chip" or "ChipThenSample" ? $"Thẻ màu ({w.RequestedSets})" : "Quotation",
            SampleQuantity = w.Plan is "Sample" or "ChipThenSample" ? (double)w.RequestedKg : 0,
            RealPriceQuoteDate = w.QuotedAt is null ? null : DateTime.ParseExact(w.QuotedAt, "yyyy-MM-dd", CultureInfo.InvariantCulture) };
        var history = new List<SampleRequestWorkflowEvent>(); var deliveries = new List<SampleRequestSampleTrial>();
        var plan = w.Plan; var sentInPlan = 0;
        for (var i = 0; i < w.Events.Count; i++) {
            var type = w.Events[i] switch { "SimulationLabFormulaRejected" => LabRejected, "SimulationSaleReopenedFormula" => SaleReopened, _ => w.Events[i] }; var at = Epoch.AddMinutes(i + 1);
            if (type is SampleFlowRules.ChipRequested or SampleFlowRules.KgRequested or SampleFlowRules.ChipThenSampleRequested) {
                plan = type == SampleFlowRules.ChipRequested ? "Chip" : type == SampleFlowRules.KgRequested ? "Sample" : "ChipThenSample";
                sentInPlan = 0;
            }
            if (type == SampleRequestWorkflowEvents.SampleSent) {
                var chip = plan == "Chip" || plan == "ChipThenSample" && sentInPlan == 0;
                sentInPlan++;
                deliveries.Add(new() { SampleRequestSampleTrialId = TrialId(deliveries.Count + 1), SampleRequestId = RequestId, FormulaId = FormulaId(w.FormulaStarts.Count(start => start <= i)), Formula = formula,
                    TrialNo = deliveries.Count + 1, IsActive = true, CreatedDate = at, SentDate = at, DeliveredSampleQuantityKg = chip ? 0 : w.DeliveredKg,
                    Status = SampleTrialStatus.WaitingCustomerFeedback });
            }
            if (type == SampleRequestWorkflowEvents.FeedbackRecorded && deliveries.LastOrDefault() is { } reply)
                reply.Status = SampleTrialStatus.Approved;
            history.Add(new() { CompanyId = RequestId, SampleRequestId = RequestId, FormulaId = FormulaId(w.FormulaStarts.Count(start => start <= i)),
                SampleRequestSampleTrialId = deliveries.Count == 0 ? null : TrialId(deliveries.Count), EventType = type, RecordedAt = at,
                Note = type is LabRejected or SampleRequestWorkflowEvents.LabDeclined ? s.RejectionReason : null });
        }
        var trial = deliveries.LastOrDefault();
        if (trial is not null) trial.Status = w.TrialResult == "Approved" ? SampleTrialStatus.Approved : w.TrialResult == "Failed" ? SampleTrialStatus.Failed : w.TrialResult == "Cancelled" ? SampleTrialStatus.Cancelled : SampleTrialStatus.WaitingCustomerFeedback;
        var events = history.AsEnumerable().Reverse().ToArray();
        return (request, formula, trial, deliveries.ToArray(), events, w.StandardPriceApproved ? events.FirstOrDefault(e => e.EventType == SampleFlowRules.StandardPriceApproved)?.RecordedAt : null);
    }

    internal static SimulationView Build(string id, string audience, SimulationState state)
    {
        var s = Sync(state); var w = s.Workflow!;
        var isCancelled = s.Status == "Cancelled";
        var (request, formula, trial, trials, events, priceAt) = Evidence(s);
        var confirmed = events.Any(e => e.EventType == SampleRequestWorkflowEvents.FormulaConfirmed && e.FormulaId == formula.FormulaId);
        var sentKg = trials.Any(t => t.FormulaId == formula.FormulaId && t.DeliveredSampleQuantityKg > 0);
        var needsNextFormula = sentKg && !FormulaSampleSentRules.CanRetryFailedKg(trial, request.SampleRequestId, formula.FormulaId);
        var decision = SampleFlowStateReader.ResolveDecision(request, trial, formula, events, w.StandardPriceApproved, confirmed, sentKg, priceAt, trials);
        var flow = SampleFlowStateReader.Build(request, trial, formula, events, audience == "Lab", audience == "Sale", w.StandardPriceApproved, confirmed, sentKg, priceAt, trials);
        if (flow.IsLabPaused && !isCancelled) {
            var message = Text("Lab đã từ chối công thức · chờ Sale mở lại xử lý", "Lab rejected the formula · awaiting Sale to reopen");
            var reopen = new SimulationAction("ResumeAfterLabRejection", Text("Mở lại xử lý sau khi Lab từ chối", "Reopen after Lab rejection"), Text("Ghi lý do để chuyển lại công việc đang xử lý.", "Provide a reason to resume the previous work."), "Note", ["note"]) { Group = "Outstanding" };
            var canResume = flow.AllowedActions.Contains("ResumeAfterLabRejection");
            return new(id, audience, s, [], [message], canResume ? [reopen] : [], canResume ? reopen : null, [message], message) {
                WorkCode = decision.Code, WorkRole = decision.Role, Stage = flow.Stage, Candidates = [new("1", "VU_SIM_001", 10)] };
        }
        var actions = new List<SimulationAction>();
        var saleFlow = audience == "Sale" ? flow : SampleFlowStateReader.Build(request, trial, formula, events, false, true, w.StandardPriceApproved, confirmed, sentKg, priceAt, trials);
        var repriced = w.Events.ToList().LastIndexOf(SampleFlowRules.StandardPriceApproved) > w.Events.ToList().LastIndexOf(SampleFlowRules.PriceRejected) && s.PriceRejected;
        var priceResponsePending = s.Status == "Open" && !s.PriceAccepted && (!s.PriceRejected || repriced) &&
            (saleFlow.AllowedActions.Contains("PriceNoResponse") || decision.Code is "SendQuotation" or "PriceFeedback");
        var deliveryResponsePending = s.Status == "Open" && trial is not null && SampleFlowRules.IsPending(trial.Status);
        void Add(string actionId, string vi, string en, string form = "Confirm", string[]? fields = null, bool danger = false, string group = "Other") {
            if (group == "Other" && flow.PlanActions.Contains(actionId) && !flow.AllowedActions.Contains(actionId)) group = "Supplementary";
            if (actions.All(a => a.Id != actionId)) actions.Add(new(actionId, Text(vi, en), OutcomeHint(actionId), form, fields ?? [], Danger: danger) { Group = group });
        }
        if (decision.Role == audience) switch (decision.Code) {
            case "ApproveFormula": case "ReworkFormula": case "AdjustFormulaPrice":
                Add("PrepareFormula", needsNextFormula ? "Tạo VU tiếp theo và xác nhận công thức" : w.FormulaStatus == "Rework" ? "Điều chỉnh và xác nhận lại công thức" : "Xác nhận công thức", needsNextFormula ? "Create and confirm next formula" : "Confirm formula", group: "Outstanding"); break;
            case "SendQuotation": case "PriceFeedback": Add("PriceFeedback", "Báo giá & phản hồi khách", "Quotation & customer feedback", "Quotation", group: "Outstanding"); break;
            case "FollowUpCustomer":
                Add(w.TrialKind == "Chip" ? "ChipFeedback" : "SampleFeedback", w.TrialKind == "Chip" ? "Phản hồi chip" : "Phản hồi mẫu", "Customer delivery feedback", "Feedback", ["result", "date", "note"], group: "Outstanding"); break;
        }
        if (audience == "Sale" && trial is not null && SampleFlowRules.IsPending(trial.Status) && s.Status == "Open")
            Add(w.TrialKind == "Chip" ? "ChipFeedback" : "SampleFeedback", w.TrialKind == "Chip" ? "Phản hồi chip" : "Phản hồi mẫu", "Customer delivery feedback", "Feedback", ["result", "date", "note"], group: "Outstanding");
        var allowed = flow.AllowedActions.Concat(flow.PlanActions).Distinct().ToList();
        // Same pending-delivery guard applied by ReadAsync / command validation in live PLM.
        if (trial is not null && SampleFlowRules.IsPending(trial.Status)) allowed.RemoveAll(a => a is "RequestChip" or "RequestSample" or "RequestChipThenSample" or "RequestLabFinalization");
        foreach (var action in allowed) switch (action) {
            case "Send": Add(flow.DeliveryKind == "ColorChip" ? "SendChip" : "SendKg", flow.DeliveryKind == "ColorChip" ? "Gửi chip" : "Gửi mẫu", flow.DeliveryKind == "ColorChip" ? "Send chips" : "Send samples", "Delivery", flow.DeliveryKind == "ColorChip" ? ["sets", "date"] : ["kg", "date"], group: "Outstanding"); break;
            case "FinalizeQuotation": Add(action, "Hoàn thành công thức để Sale lên đơn hàng", "Finalize formula for ordering", group: "Outstanding"); break;
            case "FinalizeChip": Add(action, "Hoàn thành công thức chip để Sale lên đơn hàng", "Finalize chip formula for ordering", group: "Outstanding"); break;
            case "ReopenChip": Add(action, "Điều chỉnh công thức chip", "Reopen chip formula", "Note", ["note"], group: "LabOther"); break;
            case "Complete": Add(action, "Hoàn thành yêu cầu", "Complete request", group: "Final"); break;
            case "RequestLabFinalization": Add(action, "Yêu cầu chốt công thức", "Request formula finalization", "Note", ["note"], group: "Outstanding"); break;
            case "RequestChipFinalization": Add(action, "Yêu cầu chốt công thức", "Request formula finalization", "Delivery", ["note"], group: "Outstanding"); break;
            case "RequestChip": Add(action, "Yêu cầu gửi chip", "Request chips", "Delivery", ["sets", "note"]); break;
            case "RequestSample": Add(action, "Yêu cầu chạy mẫu kg", "Request kg samples", "Delivery", ["kg", "note"]); break;
            case "RequestChipThenSample": Add(action, "Yêu cầu chip rồi mẫu kg", "Request chips then kg samples", "Delivery", ["sets", "kg", "note"]); break;
            case "RequestRequote": Add(action, "Yêu cầu báo giá lại", "Request requotation", "Note", ["note"]); break;
            case "RequestLowerPrice": Add(action, "Yêu cầu điều chỉnh giá", "Request price adjustment", "Note", ["note"]); break;
            case "WaitCustomerReply": Add(action, "Tiếp tục theo dõi phản hồi giá", "Follow up price feedback", "Note", ["note"]); break;
            case "WaitChipReply": Add(action, "Tiếp tục theo dõi khách sau chip đạt", "Follow up accepted chip", "Note", ["note"]); break;
            case "WaitRequest": Add(action, "Tiếp tục theo dõi yêu cầu", "Follow up request", "Note", ["note"]); break;
            case "CancelRejectedRequest": Add(action, "Hủy yêu cầu mẫu", "Cancel sample request", "Note", ["note"], true, "Supplementary"); break;
            case "PriceRejected": case "PriceNoResponse":
                Add("PriceFeedback", "Báo giá & phản hồi khách", "Quotation & customer feedback", "Quotation", group: priceResponsePending ? "Outstanding" : "Other"); break;
        }
        if (audience == "Sale" && s.Status is not ("Cancelled" or "Completed" or "LabDeclined"))
            Add("CancelRejectedRequest", "Hủy yêu cầu mẫu", "Cancel sample request", "Note", ["note"], true, "Supplementary");
        if (audience == "Lab" && flow.AllowedActions.Contains("RejectFormula"))
            Add("RejectFormula", "Lab từ chối yêu cầu", "Lab declines request", "Note", ["note"], true, "LabOther");
        var task = TaskTitle(decision.Code, w);
        var complete = saleFlow.AllowedActions.Contains("Complete");
        var completionReasons = saleFlow.CompletionBlockers.Select(CompletionReason).ToArray();
        if (audience == "Sale" && s.Status == "Open" && saleFlow.CompletionVisible)
        {
            Add("Complete", "Hoàn thành yêu cầu", "Complete request", group: "Final");
            for (var i = 0; i < actions.Count; i++)
                if (actions[i].Id == "Complete") actions[i] = actions[i] with { Enabled = complete, DisabledReasons = completionReasons };
        }
        if (isCancelled) actions.Clear();
        var primary = actions.Where(a => a.Group == "Outstanding" && a.Enabled)
            .OrderBy(a => a.Id == "PriceFeedback" ? 0 : a.Id is "SampleFeedback" or "SendKg" ? 1 : a.Id is "ChipFeedback" or "SendChip" ? 2 : 3)
            .FirstOrDefault() ?? actions.FirstOrDefault(a => a.Id == "Complete" && a.Enabled);
        var previousDate = s.QuotationFeedback?.FeedbackDate ?? "2026-11-14";
        var pendingSale = new List<SimulationText>();
        if (priceResponsePending) pendingSale.Add(TaskTitle(w.QuotedAt is null ? "SendQuotation" : "PriceFeedback", w));
        if (deliveryResponsePending) pendingSale.Add(TaskTitle("FollowUpCustomer", w));
        if (decision.Role == "Sale" && decision.Code is not ("ChooseAfterPrice" or "ChooseAfterChip" or "TrackApprovedChip" or "TrackRequest" or "CompleteRequest") &&
            (pendingSale.Count == 0 || decision.Code is not ("SendQuotation" or "PriceFeedback" or "FollowUpCustomer"))) pendingSale.Add(task);
        return new(id, audience, s, decision.Role == "Lab" ? [task] : [], pendingSale, actions, primary,
            !complete && s.Status == "Open" ? completionReasons : [], null) {
            WorkCode = decision.Code, WorkRole = decision.Role, Stage = flow.Stage,
            PendingDeliveryPlan = s.Status == "Open" && (SampleRequestPlanRules.HasOutstandingDelivery(events) ||
                ChipThenSampleRules.CanContinueWithSample(request, trial, events, trials)) ? new(w.Plan, w.RequestedSets, w.RequestedKg) : null,
            Candidates = [new("1", "VU_SIM_001", 10)],
            Quotation = SimulationCatalog.Quotation(id, s) with { QuotedAt = w.QuotedAt,
                ApprovedAt = priceAt.HasValue ? repriced ? DateTime.ParseExact(previousDate, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddSeconds(1).ToString("O") : "2026-11-09" : null,
                CanConfirmCustomerPrice = !isCancelled && audience == "Sale" && confirmed && w.StandardPriceApproved && !s.PriceAccepted,
                DateEditable = !isCancelled && audience == "Sale" && confirmed && w.StandardPriceApproved,
                AllowedActions = isCancelled ? [] : flow.AllowedActions, PrimaryActionCode = decision.Code, IsActionForCurrentUser = !isCancelled && decision.Role == audience },
            ExternalActions = !isCancelled && decision.Role == "President" ? [new("ApproveStandardPrice", Text("Mô phỏng BGĐ xác nhận giá chuẩn", "Simulate management price approval"), Text("Chuyển mốc BGĐ trong dữ liệu mô phỏng; không phải quyền của Sale/Lab.", "Advance the synthetic management stage; this is not a Sale/Lab permission."), "Confirm", [])] : []
        };
    }

    private static SimulationText CompletionReason(string code) => code switch {
        "PriceAcceptanceRequired" => Text("Chưa có phản hồi giá đạt", "Customer price acceptance missing"),
        "SampleAcceptanceRequired" => Text("Chưa có mẫu kg đạt", "Accepted kg sample missing"),
        "ChipAcceptanceRequired" => Text("Chưa có phản hồi chip đạt", "Chip acceptance missing"),
        "PendingDelivery" => Text("Lab chưa gửi đủ yêu cầu đang chờ", "Requested Lab delivery is pending"),
        "FormulaFinalizationRequired" => Text("Lab chưa chốt công thức", "Lab formula finalization is pending"),
        "LabPaused" => Text("Yêu cầu đang bị Lab từ chối", "Request paused by Lab"),
        "RequestClosed" => Text("Yêu cầu đã đóng", "Request is closed"),
        _ => Text("Chưa đủ điều kiện hoàn thành theo quy trình", "Workflow completion requirements are not met"),
    };

    private static SimulationText OutcomeHint(string action) => action switch {
        "RejectFormula" => Text("Ghi lý do, đánh dấu Lab từ chối và báo nhóm Chung.", "Provide a reason, decline the request and notify General."),
        "PrepareFormula" => Text("Ghi nhận công thức đã xác nhận; việc tiếp theo được xác định lại theo giá và kế hoạch gửi hiện tại.", "Confirm the formula; resolve the next assignment from pricing and the current delivery plan."),
        "SendChip" => Text("Ghi lần gửi chip. Sale ghi phản hồi; kế hoạch chip rồi kg vẫn cho phép Lab tiếp tục bước mẫu kg.", "Record chip delivery. Sale records feedback; a chips-then-kg plan can continue with kg preparation."),
        "SendKg" => Text("Ghi lần gửi mẫu kg và chuyển sang chờ Sale ghi phản hồi khách.", "Record kg delivery and await Sale's customer feedback."),
        "PriceFeedback" => Text("Lưu ngày đã báo giá và phản hồi giá. Đồng ý giá không tự hoàn thành yêu cầu.", "Record quotation date and price feedback. Price acceptance does not complete the request."),
        "ChipFeedback" or "SampleFeedback" => Text("Đạt, không đạt hoặc khách hủy sẽ cập nhật bước xử lý theo luồng PLM hiện tại.", "Acceptance, rejection or cancellation updates the next assignment using current PLM rules."),
        "RequestLabFinalization" or "RequestChipFinalization" => Text("Lab chốt đúng VU để hoàn thành yêu cầu.", "Lab finalizes the requested formula and completes the request."),
        "FinalizeQuotation" or "FinalizeChip" => Text("Chốt VU và hoàn thành yêu cầu; không tự tạo đơn hàng.", "Finalize the formula and complete the request; no order is created."),
        "Complete" => Text("Chốt yêu cầu và công thức được chọn để lên đơn; không tự tạo đơn hàng.", "Complete the request and select its formula for ordering; no order is created."),
        "RequestRequote" => Text("Yêu cầu BGĐ báo giá lại; giữ nguyên kết quả và kế hoạch hiện tại.", "Request management pricing; preserve current results and delivery plan."),
        "RequestChip" or "RequestSample" or "RequestChipThenSample" => Text("Cập nhật kế hoạch trên cùng TP; giữ lịch sử lần gửi và xác định lại việc Lab cần làm.", "Update the plan on this request, retain delivery history and resolve Lab's next task."),
        "RequestLowerPrice" => Text("Gửi yêu cầu điều chỉnh giá để Lab xử lý công thức; giữ phản hồi giá cũ trong lịch sử.", "Ask Lab to revise the formula for pricing; retain previous price feedback."),
        "CancelRejectedRequest" => Text("Hủy yêu cầu và giữ lý do, lịch sử xử lý.", "Cancel the request and retain its reason and history."),
        "ReopenChip" => Text("Mở lại công thức chip để Lab điều chỉnh theo luồng hiện tại.", "Reopen the chip formula for Lab revision."),
        _ => Text("Ghi nhận lựa chọn theo dõi và cập nhật việc cần làm.", "Record the follow-up choice and update the assignment.") };

    private static SimulationText TaskTitle(string code, SimulationWorkflow w) => code switch {
        "ApproveFormula" => Text("Lab xác nhận công thức", "Lab confirms formula"), "ReworkFormula" => Text("Lab điều chỉnh công thức và gửi lại", "Lab reworks and sends again"),
        "AdjustFormulaPrice" => Text("Lab điều chỉnh công thức theo yêu cầu giá", "Lab revises formula for price adjustment"),
        "ApproveStandardPrice" => Text("Chờ BGĐ xác nhận giá chuẩn", "Awaiting management standard price approval"),
        "SendSample" => Text(w.Plan == "Chip" || w.Plan == "ChipThenSample" && w.TrialKind == "" ? "Gửi chip" : "Gửi mẫu kg", "Record delivery"),
        "SendQuotation" => Text("Sale ghi ngày đã báo giá", "Sale records quotation date"), "PriceFeedback" => Text("Sale ghi phản hồi giá", "Sale records price feedback"),
        "FollowUpCustomer" => Text(w.TrialKind == "Chip" ? "Sale ghi phản hồi chip" : "Sale ghi phản hồi mẫu", "Sale records delivery feedback"),
        "ChooseAfterPrice" => Text("Sale chọn bước tiếp theo sau khi khách đồng ý giá", "Sale chooses next step after price acceptance"),
        "ChooseAfterChip" => Text("Sale chọn bước tiếp theo sau chip đạt", "Sale chooses next step after chip acceptance"),
        "FinalizeQuotation" or "FinalizeChip" => Text("Lab hoàn thành công thức để Sale lên đơn hàng", "Lab finalizes formula for ordering"),
        "CompleteRequest" => Text("Sale hoàn thành yêu cầu", "Sale completes request"),
        "ChooseRejectedPriceAction" => Text("Sale chọn hướng xử lý giá không đạt", "Sale chooses how to handle rejected price"),
        "TrackRejectedPrice" or "TrackApprovedChip" or "TrackRequest" => Text("Sale tiếp tục theo dõi khách hàng", "Sale follows up customer"),
        "" => Text("Yêu cầu đã kết thúc", "Request closed"), _ => Text("Cần kiểm tra hồ sơ", "Review request") };

    internal static SimulationView Evaluate(SimulationStepRequest request, SimulationScenario scenario, SimulationState state)
    {
        Validate(state.Workflow);
        var view = Build(scenario.Id, request.Audience, state);
        if (string.IsNullOrEmpty(request.ActionId)) return view;
        var action = view.Actions.Concat(view.ExternalActions).SingleOrDefault(a => a.Id == request.ActionId) ?? throw new ArgumentException("SimulationActionUnavailable");
        if (!action.Enabled) throw new ArgumentException("SimulationActionUnavailable");
        var input = request.Input;
        if ((input.Note?.Length ?? 0) > 2000 || action.Form == "Note" && string.IsNullOrWhiteSpace(input.Note) ||
            action.Fields.Contains("sets") && input.Sets is not (>= 1 and <= 30) || action.Fields.Contains("kg") && input.Kg is not (> 0 and <= 999999999) ||
            action.Fields.Contains("date") && !DateOnly.TryParseExact(input.Date, "yyyy-MM-dd", out _)) throw new ArgumentException("InvalidSimulationInput");
        var w = state.Workflow!;
        void Event(string e) => w = w with { Events = w.Events.Append(e).ToArray() };
        switch (action.Id) {
            case "RejectFormula":
                state = state with { Status = "LabDeclined", RejectedFromStatus = state.Status, RejectionReason = input.Note!.Trim(),
                    Notifications = state.Notifications.Append(new SimulationNotification("General", "Lab", Text("Lab từ chối yêu cầu", "Lab declined the request"), input.Note.Trim())).ToArray() };
                Event(SampleRequestWorkflowEvents.LabDeclined); break;
            case "ResumeAfterLabRejection":
                state = state with { Status = "Open", Notifications = state.Notifications.Append(new SimulationNotification("General", "Sale", Text("Sale mở lại xử lý sau khi Lab từ chối", "Sale reopened processing after Lab rejection"), input.Note!.Trim())).ToArray() };
                Event(SaleReopened); break;
            case "PrepareFormula":
                var evidence = Evidence(state);
                if (evidence.Trials.Any(t => t.FormulaId == evidence.Formula.FormulaId && t.DeliveredSampleQuantityKg > 0) &&
                    !FormulaSampleSentRules.CanRetryFailedKg(evidence.Trial, evidence.Request.SampleRequestId, evidence.Formula.FormulaId)) {
                    w = w with { FormulaStarts = w.FormulaStarts.Append(w.Events.Count).ToArray() };
                    state = state with { QuotationFeedback = null };
                }
                w = w with { FormulaStatus = "Approved" }; Event(SampleRequestWorkflowEvents.FormulaConfirmed); break;
            case "ApproveStandardPrice": w = w with { StandardPriceApproved = true }; Event(SampleFlowRules.StandardPriceApproved); break;
            case "SendChip": case "SendKg":
                w = w with { TrialKind = action.Id == "SendChip" ? "Chip" : "Sample", TrialResult = "", TrialNo = w.TrialNo + 1, SentDate = input.Date!, ReplyDate = null, DeliveredKg = input.Kg ?? 0,
                    FormulaStatus = action.Id == "SendKg" ? "SampleSent" : "Approved" }; Event(SampleRequestWorkflowEvents.SampleSent); break;
            case "ChipFeedback": case "SampleFeedback":
                if (input.Result is not ("Approved" or "Failed" or "Waiting" or "Cancelled") || string.IsNullOrWhiteSpace(input.Note) || string.CompareOrdinal(input.Date, w.SentDate) < 0) throw new ArgumentException("InvalidSimulationFeedback");
                if (input.Result != "Waiting") {
                    w = w with { TrialResult = input.Result!, ReplyDate = input.Date, FormulaStatus = input.Result == "Failed" ? "Rework" : w.TrialKind == "Chip" ? "Approved" : "SampleSent" };
                    Event(SampleRequestWorkflowEvents.FeedbackRecorded);
                    if (action.Id == "SampleFeedback" && input.Result == "Approved")
                    {
                        w = w with { FormulaStatus = "Completed" };
                        state = state with { Status = "Completed" };
                    }
                    if (input.Result == "Cancelled") { state = state with { Status = "Cancelled" }; Event(SampleRequestWorkflowEvents.RequestCancelled); }
                }
                break;
            case "PriceFeedback":
                var q = input.QuotationFeedback ?? throw new ArgumentException("InvalidSimulationFeedback");
                SimulationTransitions.ValidateQuotationFeedback(q);
                if (!DateOnly.TryParseExact(q.Date, "yyyy-MM-dd", out _) || !DateOnly.TryParseExact(q.FeedbackDate, "yyyy-MM-dd", out _) ||
                    q.Outcome is not ("Accepted" or "Rejected" or "NoResponse") || q.Note is null || q.Note.Length > 2000 || q.PrivateNote is null || q.PrivateNote.Length > 2000 ||
                    q.Outcome != "NoResponse" && (string.IsNullOrWhiteSpace(q.Note) || string.CompareOrdinal(q.FeedbackDate, q.Date) < 0) ||
                    state.PriceAccepted && q.Outcome != "Accepted") throw new ArgumentException("InvalidSimulationFeedback");
                w = w with { QuotedAt = q.Date }; state = state with { QuotationFeedback = q };
                Event(q.Outcome == "Accepted" ? SampleRequestWorkflowEvents.PriceAccepted : q.Outcome == "Rejected" ? SampleFlowRules.PriceRejected : SampleFlowRules.PriceNoResponse); break;
            case "RequestChip": case "RequestSample": case "RequestChipThenSample":
                var plan = action.Id == "RequestChip" ? "Chip" : action.Id == "RequestSample" ? "Sample" : "ChipThenSample";
                if (view.PendingDeliveryPlan is { } pending && pending.Plan == plan &&
                    (plan == "Sample" || pending.Sets == input.Sets) && (plan == "Chip" || pending.Kg == input.Kg))
                    throw new ArgumentException("SimulationPlanUnchanged");
                var currentEvidence = Evidence(state);
                var canResumeFormula = !currentEvidence.Trials.Any(t => t.FormulaId == currentEvidence.Formula.FormulaId && t.DeliveredSampleQuantityKg > 0);
                w = w with { Plan = action.Id == "RequestChip" ? "Chip" : action.Id == "RequestSample" ? "Sample" : "ChipThenSample", RequestedSets = input.Sets ?? w.RequestedSets, RequestedKg = input.Kg ?? w.RequestedKg,
                    FormulaStatus = w.FormulaStatus == "Completed" && canResumeFormula ? "Approved" : w.FormulaStatus };
                state = state with { Status = "Open" };
                Event(action.Id == "RequestChip" ? SampleFlowRules.ChipRequested : action.Id == "RequestSample" ? SampleFlowRules.KgRequested : SampleFlowRules.ChipThenSampleRequested); break;
            // Live RequestRequote only sends the management pricing request; it does not
            // revoke acceptance, reopen the request or rewrite the delivery plan.
            case "RequestRequote": break;
            case "RequestLowerPrice": Event(SampleFlowRules.LowerPriceRequested); break;
            case "RequestLabFinalization": Event(SampleFlowRules.LabFinalizationRequested); break;
            case "RequestChipFinalization": Event(SampleFlowRules.ChipFinalizationRequested); break;
            case "WaitChipReply": Event(SampleFlowRules.ChipFollowUpDeferred); break;
            case "WaitCustomerReply": Event(SampleFlowRules.PriceFollowUpDeferred); break;
            case "WaitRequest": Event(SampleFlowRules.CompletionDeferred); break;
            case "FinalizeQuotation":
            case "FinalizeChip":
                var finalization = Evidence(state);
                if (SaleRequestedFinalizationRules.Validate(finalization.Request, finalization.Formula.FormulaId,
                    action.Id, finalization.Events, finalization.Trials) is not null)
                    throw new ArgumentException("SimulationActionUnavailable");
                w = w with { FormulaStatus = "Completed" };
                state = state with { Status = "Completed" };
                Event(action.Id == "FinalizeChip" ? SampleFlowRules.ChipFinalized : SampleFlowRules.QuotationFinalized); break;
            case "ReopenChip": w = w with { FormulaStatus = "Rework" }; Event(SampleFlowRules.ChipReopened); break;
            case "Complete": w = w with { FormulaStatus = "Completed" }; state = state with { Status = "Completed" }; Event(SampleFlowRules.CompletedWithoutTrial); break;
            case "CancelRejectedRequest": state = state with { Status = "Cancelled" }; Event(SampleRequestWorkflowEvents.RequestCancelled); break;
            default: throw new ArgumentException("SimulationActionUnavailable");
        }
        var next = Sync(state with { Workflow = w, Note = input.Note?.Trim() ?? state.Note,
            History = state.History.Append(new SimulationHistory(action.Id, input.Note?.Trim() ?? "", view.State.Status, state.Status, action.Title)).ToArray() });
        Validate(next.Workflow);
        return Build(scenario.Id, request.Audience, next);
    }
    private static void Validate(SimulationWorkflow? w)
    {
        if (w is null || w.Plan is not ("Quotation" or "Chip" or "Sample" or "ChipThenSample") ||
            w.FormulaName is null || w.FormulaName.Length > 200 || w.FormulaNote is null || w.FormulaNote.Length > 2000 ||
            w.FormulaStatus is not ("Draft" or "Approved" or "Rework" or "SampleSent" or "Completed") ||
            w.TrialKind is not ("" or "Chip" or "Sample") || w.TrialResult is not ("" or "Approved" or "Failed" or "Cancelled") ||
            w.TrialNo is < 0 or > 1000 || w.RequestedSets is < 1 or > 30 || w.RequestedKg is <= 0 or > 999999999 || w.DeliveredKg is < 0 or > 999999999 ||
            !DateOnly.TryParseExact(w.SentDate, "yyyy-MM-dd", out _) || w.ReplyDate is not null && !DateOnly.TryParseExact(w.ReplyDate, "yyyy-MM-dd", out _) ||
            w.Events is null || w.Events.Count > 2000 || w.Events.Any(e => !KnownEvents.Contains(e)) ||
            w.FormulaStarts is null || w.FormulaStarts.Count is < 1 or > 100 || w.FormulaStarts[0] != 0 ||
            w.FormulaStarts.Any(start => start < 0 || start > w.Events.Count) || !w.FormulaStarts.SequenceEqual(w.FormulaStarts.Distinct().Order()) ||
            w.QuotedAt is not null && !DateOnly.TryParseExact(w.QuotedAt, "yyyy-MM-dd", out _)) throw new ArgumentException("InvalidSimulationState");
    }
}
