using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using static HRM.Application.Features.Development.SampleWorkflowSimulation.SimulationCatalog;

namespace HRM.Application.Features.Development.SampleWorkflowSimulation;

internal static class BusinessSimulationCatalog
{
    public static IReadOnlyList<SimulationScenario> All()
    {
        var cases = new List<SimulationScenario>();
        foreach (var (prefix, plan, vi, en) in new[] {
            ("quote", "Quotation", "Chỉ báo giá", "Quotation only"),
            ("chip", "Chip", "Báo giá + chip", "Quotation + chips"),
            ("sample", "Sample", "Báo giá + mẫu kg", "Quotation + kg samples"),
            ("both", "ChipThenSample", "Báo giá + chip rồi mẫu kg", "Quotation + chips then kg samples") })
        {
            var order = 0;
            SimulationWorkflow w = new() { Plan = plan, Events = plan switch {
                "Chip" => [SampleFlowRules.QuotationRequired, SampleFlowRules.ChipRequested],
                "Sample" => [SampleFlowRules.QuotationRequired, SampleFlowRules.KgRequested],
                "ChipThenSample" => [SampleFlowRules.QuotationRequired, SampleFlowRules.ChipThenSampleRequested], _ => [] } };
            void Add(string id, string nameVi, string nameEn, string status = "Open")
            {
                var state = BusinessSimulation.Sync(new SimulationState { Workflow = w, Status = status });
                cases.Add(new($"{prefix}-{id}", Text($"{++order:00}. {vi} · {nameVi}", $"{order:00}. {en} · {nameEn}"), state) { Group = plan });
            }
            void Event(string type) => w = w with { Events = w.Events.Append(type).ToArray() };
            Add("new", "Lab chưa xác nhận công thức", "Formula awaiting Lab confirmation");
            Event(SampleRequestWorkflowEvents.FormulaConfirmed); w = w with { FormulaStatus = "Approved" };
            Add("confirmed", "Đã xác nhận công thức · chưa có giá chuẩn", "Formula confirmed · standard price pending");
            Event(SampleFlowRules.StandardPriceApproved); w = w with { StandardPriceApproved = true };
            Add("priced", "Đã có giá chuẩn · Sale chưa báo giá", "Standard price approved · quotation not recorded");
            w = w with { QuotedAt = "2026-11-10" }; Event(SampleFlowRules.PriceNoResponse);
            Add("quoted", "Đã báo giá · chờ phản hồi giá", "Quoted · awaiting price feedback");
            var quoted = w;
            Event(SampleFlowRules.PriceRejected); Add("price-rejected", "Khách không đồng ý giá · chọn hướng xử lý", "Price rejected · choose next step");
            Event(SampleFlowRules.PriceFollowUpDeferred); Add("price-follow-up", "Tiếp tục theo dõi phản hồi giá", "Following up on price rejection");
            Event(SampleFlowRules.LowerPriceRequested); w = w with { StandardPriceApproved = false, FormulaStatus = "Rework" };
            Add("price-rework", "Yêu cầu điều chỉnh giá · Lab chỉnh công thức", "Price adjustment · Lab revises formula");
            Event(SampleRequestWorkflowEvents.FormulaConfirmed); w = w with { FormulaStatus = "Approved" };
            Add("price-reconfirm", "Lab xác nhận lại · chờ duyệt giá mới", "Formula reconfirmed · new standard price pending");
            Event(SampleFlowRules.StandardPriceApproved); w = w with { StandardPriceApproved = true };
            Add("repriced", "Đã duyệt giá mới · ghi lại phản hồi khách", "New price approved · record customer response");
            w = quoted; Event(SampleRequestWorkflowEvents.PriceAccepted);
            Add("price-accepted", "Khách đồng ý giá", "Customer accepted price");
            if (plan == "Quotation")
            {
                Event(SampleFlowRules.LabFinalizationRequested); Add("finalize", "Sale yêu cầu hoàn thành công thức để lên đơn", "Sale requested formula finalization for ordering");
                Event(SampleFlowRules.QuotationFinalized); w = w with { FormulaStatus = "Completed" };
                Add("order-ready", "Lab đã chốt · yêu cầu hoàn thành", "Lab finalized · request completed", "Completed");
            }
            else
            {
                var accepted = w;
                void Send(string kind) {
                    Event(SampleRequestWorkflowEvents.SampleSent);
                    w = w with { TrialKind = kind, TrialNo = w.TrialNo + 1, TrialResult = "", FormulaStatus = kind == "Sample" ? "SampleSent" : "Approved" };
                }
                var firstKind = plan == "Sample" ? "Sample" : "Chip";
                Send(firstKind); Add("sent", firstKind == "Chip" ? "Đã gửi chip · chờ khách phản hồi" : "Đã gửi mẫu kg · chờ khách phản hồi", "Delivered · awaiting customer feedback");
                var sent = w;
                w = sent with { StandardPriceApproved = false, QuotedAt = null, Events = sent.Events.Where(e => e is not SampleRequestWorkflowEvents.PriceAccepted and not SampleFlowRules.StandardPriceApproved and not SampleFlowRules.PriceNoResponse).ToArray() };
                Add("sent-no-price", "Đã gửi · chưa có giá chuẩn", "Delivered · standard price pending");
                w = sent with { QuotedAt = null, Events = sent.Events.Where(e => e is not SampleRequestWorkflowEvents.PriceAccepted and not SampleFlowRules.PriceNoResponse).ToArray() };
                Add("sent-not-quoted", "Đã gửi · có giá chuẩn · chưa báo giá", "Delivered · standard price approved · not quoted");
                w = sent with { Events = sent.Events.Where(e => e != SampleRequestWorkflowEvents.PriceAccepted).ToArray() };
                Add("sent-price-waiting", "Đã gửi · chờ phản hồi cả giá và chip / mẫu", "Delivered · price and delivery feedback pending");
                Event(SampleFlowRules.PriceRejected);
                Add("sent-price-rejected", "Đã gửi · khách không đồng ý giá · chưa phản hồi chip / mẫu", "Delivered · price rejected · delivery response pending");
                w = sent;
                w = w with { TrialResult = "Cancelled" }; Event(SampleRequestWorkflowEvents.RequestCancelled);
                Add("customer-cancelled", "Khách hủy sau khi nhận chip / mẫu", "Customer cancelled after delivery", "Cancelled");
                w = sent;
                w = w with { TrialResult = "Failed", FormulaStatus = "Rework" };
                Event(SampleRequestWorkflowEvents.FeedbackRecorded); Add("failed", "Khách phản hồi không đạt · Lab làm tiếp", "Customer rejected delivery · Lab reworks");
                if (firstKind == "Sample") w = w with { FormulaStarts = w.FormulaStarts.Append(w.Events.Count).ToArray(), StandardPriceApproved = false };
                Event(SampleRequestWorkflowEvents.FormulaConfirmed); w = w with { FormulaStatus = "Approved" };
                Add("retry", firstKind == "Chip" ? "Đã chỉnh cùng VU · gửi lại chip" : "Đã xác nhận VU tiếp theo · gửi lại mẫu kg", "Reconfirmed formula · send next trial");
                w = sent with { TrialResult = "Approved", FormulaStatus = firstKind == "Chip" ? "Approved" : "SampleSent" };
                Event(SampleRequestWorkflowEvents.FeedbackRecorded); Add("approved", "Khách phản hồi đạt · chọn bước tiếp theo", "Delivery accepted · choose next step");
                if (plan == "Chip") {
                    Event(SampleFlowRules.ChipFollowUpDeferred); Add("follow-up", "Chip đạt · Sale tiếp tục theo dõi", "Accepted chip · Sale follows up");
                    Event(SampleFlowRules.ChipFinalizationRequested); Add("finalize", "Sale yêu cầu Lab hoàn thành công thức", "Sale requested chip formula finalization");
                    Event(SampleFlowRules.ChipFinalized); w = w with { FormulaStatus = "Completed" };
                    Add("ready", "Lab đã chốt · yêu cầu hoàn thành", "Lab finalized · request completed", "Completed");
                }
                else {
                    if (plan == "ChipThenSample") {
                        Send("Sample"); Add("sample-sent", "Đã gửi mẫu kg sau chip · chờ phản hồi", "Kg sample sent after chips · awaiting feedback");
                        var kgSent = w;
                        w = w with { TrialResult = "Failed", FormulaStatus = "Rework" }; Event(SampleRequestWorkflowEvents.FeedbackRecorded);
                        Add("sample-failed", "Mẫu kg không đạt · Lab làm tiếp", "Kg sample rejected · Lab reworks");
                        w = kgSent with { TrialResult = "Approved" }; Event(SampleRequestWorkflowEvents.FeedbackRecorded);
                        Add("sample-approved", "Mẫu kg đạt · Sale hoàn thành yêu cầu", "Kg sample accepted · Sale completes request");
                    }
                    var approved = w;
                    w = w with { Events = w.Events.Where(e => e != SampleRequestWorkflowEvents.PriceAccepted).ToArray() };
                    Add("sample-price-pending", "Mẫu kg đạt · còn chờ khách đồng ý giá", "Kg sample accepted · price acceptance pending");
                    w = approved;
                }
                // Cover parallel pricing rather than pretending delivery waits for price acceptance.
                var finished = w;
                w = accepted with { StandardPriceApproved = false, Events = accepted.Events.Where(e => e is not SampleRequestWorkflowEvents.PriceAccepted and not SampleFlowRules.StandardPriceApproved).ToArray() };
                Add("delivery-no-price", "Công thức đã xác nhận · gửi khi chưa có giá chuẩn", "Confirmed formula · delivery before standard price");
                w = finished;
            }
            Add("completed", "Yêu cầu đã hoàn thành · yêu cầu bổ sung", "Request completed · supplementary work", "Completed");
            Add("cancelled", "Yêu cầu đã hủy", "Request cancelled", "Cancelled");
        }
        return cases;
    }
}
