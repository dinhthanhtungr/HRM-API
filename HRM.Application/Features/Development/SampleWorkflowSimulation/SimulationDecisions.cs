using static HRM.Application.Features.Development.SampleWorkflowSimulation.SimulationCatalog;

namespace HRM.Application.Features.Development.SampleWorkflowSimulation;

/// <summary>Proposed simulation rules, deliberately independent of production PLM mutation handlers.</summary>
public static class SimulationDecisions
{
    public static bool NeedsSample(SimulationState s) => s.SampleRequired || s.Combined || s.SampleKg > 0 || s.Batches.Count > 0;
    public static bool NeedsChip(SimulationState s) => s.ChipRequired || s.Combined || s.ChipSets > 0 || s.ChipSent;
    public static SimulationView Build(string id, string audience, SimulationState s)
    {
        var lab = new List<SimulationText>(); var sale = new List<SimulationText>(); var blockers = new List<SimulationText>();
        if (s.QuotePending) lab.Add(Text("Chuẩn bị / duyệt báo giá", "Prepare / approve quotation"));
        if (s.ChipSets > 0) lab.Add(Text($"Gửi chip · {s.ChipSets} bộ", $"Send chips · {s.ChipSets} sets"));
        if (s.SampleKg > 0) lab.Add(Text($"Gửi mẫu · {s.SampleKg} kg" + (s.Combined && s.ChipSets > 0 ? " · Sau bước chip" : ""), $"Send sample · {s.SampleKg} kg" + (s.Combined && s.ChipSets > 0 ? " · After chip delivery" : "")));
        if (s.QuoteReady) sale.Add(Text("Gửi báo giá đã đủ điều kiện", "Send the prepared quotation"));
        if (s.PricePending) sale.Add(Text("Phản hồi giá", "Record price feedback"));
        if (s.PriceRejected) sale.Add(Text("Giá chưa được đồng ý · chọn hướng xử lý", "Price rejected · choose next step"));
        foreach (var b in s.Batches.Where(b => b.Options.All(o => o.Result == ""))) sale.Add(Text($"Phản hồi mẫu đợt {b.Number}", $"Sample feedback for batch {b.Number}"));
        if (s.ChipSent && !s.ChipFeedbackDone) sale.Add(NeedsSample(s) ? Text("Phản hồi chip · tùy chọn", "Chip feedback · optional") : Text("Phản hồi chip", "Chip feedback"));
        if (lab.Count > 0) blockers.Add(Text("Còn công việc chờ Lab", "Laboratory work is still pending"));
        if (s.QuoteReady) blockers.Add(Text("Chưa gửi báo giá", "Quotation not sent"));
        if (s.PriceRequired && !s.PriceAccepted) blockers.Add(Text("Thiếu giá được khách chấp nhận", "Customer price acceptance missing"));
        if (!s.FormulaReady) blockers.Add(Text("Công thức chưa đủ điều kiện", "Formula is not ready"));
        if (NeedsSample(s)) {
            if (s.Batches.LastOrDefault()?.Options.Any(o => o.Result != "") != true) blockers.Add(Text("Chưa ghi nhận phản hồi mẫu đợt hiện tại", "Current sample batch has no response"));
            if (s.Batches.LastOrDefault()?.Options.Any(o => o.Result == "Approved") != true) blockers.Add(Text("Chưa có mẫu đạt trong đợt hiện tại", "No accepted option in the current batch"));
        }
        else if (NeedsChip(s) && s.ChipResult != "Approved") blockers.Add(Text("Chip chưa được khách xác nhận đạt", "Chip acceptance is missing"));
        SimulationText? warning = s.UnresolvedRule switch {
            "OldBatchPolicy" => Text("Cần chốt: có bắt buộc phản hồi mọi đợt cũ trước khi hoàn thành hay không?", "Unresolved: must all old batches receive feedback before completion?"),
            "LegacyEvidence" => Text("Thiếu bằng chứng kế hoạch / lần gửi; không tự suy đoán trạng thái.", "Plan / delivery evidence is incomplete; do not infer its state."), _ => null };
        if (warning is not null) blockers.Add(Text("Có quy tắc chưa chốt", "An unresolved rule needs a decision"));
        var actions = Actions(s, audience, blockers.Count == 0).Select(a => a with {
            Enabled = a.Id != "Complete" || blockers.Count == 0,
            DisabledReasons = a.Id == "Complete" ? blockers.ToArray() : [],
            Group = a.Id == "Supplement" ? "Internal" : s.Status == "Completed" && a.Id.StartsWith("Request") ? "Supplementary" :
                a.Id is "Complete" or "Cancel" or "Reject" ? "Final" :
                a.Id is "PriceFeedback" or "SendQuote" || a.Id == "ChipFeedback" && !NeedsSample(s) && !s.ChipFeedbackDone ||
                a.Form == "BatchFeedback" && s.Batches.Single(b => b.Number == a.BatchNumber).Options.All(o => o.Result == "") ? "Outstanding" : "Other"
        }).ToArray();
        if (s.Status != "Open") { lab.Clear(); sale.Clear(); }
        if (s.Status == "Completed") blockers.Clear();
        var enabled = actions.Where(a => a.Enabled && a.Group != "Internal").ToArray();
        var primary = enabled.FirstOrDefault(a => a.Id is "SendChip" or "SendKg" or "PrepareFormula" or "PrepareQuote")
            ?? enabled.FirstOrDefault(a => a.Id is "PriceFeedback" or "SendQuote")
            ?? enabled.FirstOrDefault(a => a.Id.StartsWith("SampleFeedback:"))
            ?? enabled.FirstOrDefault(a => a.Id == "ChipFeedback" && a.Group == "Outstanding")
            ?? enabled.FirstOrDefault(a => a.Id is "Complete" or "Resume")
            ?? enabled.FirstOrDefault(a => a.Id is "EditPending" or "RequestChip" or "RequestKg" or "RequestQuote");
        return new(id, audience, s, lab, sale, actions, primary, blockers, warning) { Quotation = SimulationCatalog.Quotation(id, s) };
    }

    private static IReadOnlyList<SimulationAction> Actions(SimulationState s, string role, bool complete)
    {
        var a = new List<SimulationAction>();
        void Add(string id, string vi, string en, string descVi, string descEn, string form = "Confirm", string[]? fields = null, int? batch = null, bool danger = false)
            => a.Add(new(id, Text(vi, en), Text(descVi, descEn), form, fields ?? [], batch, danger));
        if (s.Status == "Review") return a;
        if (s.Status is "Cancelled" or "Rejected") {
            Add("Resume", "Yêu cầu tiếp tục xử lý", "Resume processing", "Mở lại hồ sơ, giữ lý do và lịch sử trước đó.", "Reopen the request and preserve its previous reason and history.", "Delivery", ["note"]);
            if (s.Status == "Rejected" && role == "Sale") Add("Cancel", "Hủy yêu cầu phối mẫu", "Cancel sample request", "Bắt buộc có lý do, giữ lịch sử.", "A reason is required; preserve history.", "Note", ["note"], danger: true);
            return a;
        }
        Add("Reject", "Từ chối mẫu", "Reject sample request", "Bắt buộc ghi lý do. Có thể yêu cầu tiếp tục xử lý sau.", "A reason is required. Processing can be resumed later.", "Note", ["note"], danger: true);
        if (s.Status == "Completed") {
            if (role == "Sale") {
                Add("Supplement", "Yêu cầu bổ sung", "Request supplementary work", "Tiếp tục cùng TP, giữ kết quả đã hoàn thành.", "Continue on the same request, preserving completed results.");
                Add("RequestChip", "Yêu cầu gửi chip bổ sung", "Request supplementary chips", "Giữ kết quả đã hoàn thành, mở phần chip mới.", "Keep completed results and request new chips.", "Delivery", ["sets", "note"]);
                Add("RequestKg", "Yêu cầu chạy mẫu bổ sung", "Request supplementary samples", "Giữ lịch sử, mở phần mẫu kg mới.", "Preserve history and request new kg samples.", "Delivery", ["kg", "note"]);
                Add("RequestCombined", "Gửi chip trước, sau đó mẫu kg", "Chips followed by kg samples", "Mở hai phần việc bổ sung theo thứ tự chip rồi kg.", "Request supplementary chips followed by kg samples.", "Delivery", ["sets", "kg", "note"]);
                Add("RequestQuote", "Yêu cầu báo giá lại", "Request a new quotation", "Mở đợt báo giá mới, giữ kết quả đã hoàn thành.", "Open a new quotation cycle and preserve completed results.", "Note", ["note"]);
            }
            return a;
        }
        if (role == "Lab") {
            if (!s.FormulaReady && (s.ChipSets > 0 || s.SampleKg > 0 || s.QuotePending)) Add("PrepareFormula", "Hoàn thành công thức", "Confirm formula", "Xác nhận công thức đã sẵn sàng để chuẩn bị chip, mẫu và báo giá.", "Confirm the formula is ready for chip, sample and quotation preparation.");
            if (s.QuotePending && s.FormulaReady) Add("PrepareQuote", "Chuẩn bị báo giá", "Prepare quotation", "Chuẩn bị thông tin giá để chuyển Sale gửi khách.", "Prepare pricing information for Sale to send to the customer.");
            if (s.ChipSets > 0 && s.FormulaReady) Add("SendChip", "Gửi chip", "Send chips", "Ghi nhận chip thực tế đã gửi.", "Record the actual chip delivery.", "Delivery", ["sets", "date"]);
            if (s.SampleKg > 0 && s.FormulaReady && !(s.Combined && s.ChipSets > 0)) Add("SendKg", "Gửi mẫu", "Send samples", "Chọn thêm VU để gửi cùng đợt.", "Select additional formulas for the same delivery batch.", "SendOptions", ["date", "options"]);
            return a;
        }
        if (s.QuoteReady) Add("SendQuote", "Gửi / ghi nhận đã gửi báo giá", "Record quotation sent", "Ghi ngày gửi để theo dõi phản hồi.", "Record the date to track customer feedback.", "Delivery", ["date"]);
        if (s.PricePending) Add("PriceFeedback", "Phản hồi giá", "Price feedback", "Ghi kết quả khách phản hồi về giá.", "Record the customer's price response.", "Feedback", ["result", "date", "note"]);
        if (s.PriceAccepted || s.PriceRejected) Add("ViewPriceFeedback", "Xem báo giá và phản hồi khách", "View quotation and customer feedback", "Xem trạng thái giá; giữ khóa khi khách đã chấp nhận.", "Review pricing; accepted feedback remains locked.", "Quotation");
        foreach (var b in s.Batches) Add($"SampleFeedback:{b.Number}", b.Options.All(o => o.Result == "") ? $"Phản hồi mẫu đợt {b.Number}" : $"Xem kết quả mẫu đợt {b.Number}", $"Sample feedback · batch {b.Number}", "Ghi phản hồi và lưu chung cho cả đợt; đã lưu phải bấm bút mới sửa.", "Save feedback once for the batch; use the pencil to edit saved results.", "BatchFeedback", batch: b.Number);
        if (s.ChipSent) Add("ChipFeedback", NeedsSample(s) ? "Phản hồi chip · tùy chọn" : "Phản hồi chip", "Chip feedback", NeedsSample(s) ? "Không chặn Lab gửi mẫu kg hoặc hoàn thành khi mẫu kg và giá đạt." : "Ghi kết quả chip để hoàn thành yêu cầu chip và giá.", "Record chip feedback; optional when kg samples are required.", "Feedback", ["result", "date", "note"]);
        if (s.PriceRejected && !s.QuotePending) Add("LowerPrice", "Yêu cầu điều chỉnh giá", "Request price adjustment", "Giữ riêng công việc giá và giao mẫu.", "Keep pricing work separate from delivery work.", "Note", ["note"]);
        if (s.ChipSets == 0) Add("RequestChip", s.HasHistory ? "Yêu cầu gửi chip lần tiếp theo" : "Yêu cầu gửi chip", "Request chips", "Không thay thế phản hồi mẫu còn thiếu.", "Preserves outstanding sample feedback.", "Delivery", ["sets", "note"]);
        if (s.SampleKg == 0) Add("RequestKg", s.HasHistory ? "Yêu cầu chạy mẫu lần tiếp theo" : "Yêu cầu chạy mẫu", "Request kg samples", "Chỉ tạo phần việc chưa có yêu cầu đang chờ.", "Only request work that is not already pending.", "Delivery", ["kg", "note"]);
        if (s.ChipSets == 0 && s.SampleKg == 0) Add("RequestCombined", "Gửi chip trước, sau đó mẫu kg", "Chip delivery followed by kg samples", "Ghi cả hai phần việc từ đầu.", "Request both stages from the beginning.", "Delivery", ["sets", "kg", "note"]);
        if (s.ChipSets > 0 || s.SampleKg > 0) {
            Add("EditPending", "Sửa yêu cầu đang chờ", "Edit pending request", "Chỉ sửa phần chưa giao, không tạo lần gửi mới.", "Edit only undelivered work; no new delivery batch.", "Delivery", [.. (s.ChipSets > 0 ? new[] { "sets" } : []), .. (s.SampleKg > 0 ? new[] { "kg" } : []), "note"]);
            Add("UpdateNote", "Cập nhật lưu ý cho Lab", "Update Lab note", "Không tạo yêu cầu giao mới.", "Does not create another delivery request.", "Note", ["note"]);
        }
        if (!s.QuotePending && !s.QuoteReady) Add("RequestQuote", s.HasHistory ? "Yêu cầu báo giá lại" : "Yêu cầu chuẩn bị báo giá", "Request quotation", "Ghi nội dung yêu cầu báo giá gửi Lab. Không tạo thêm yêu cầu giao mẫu.", "Record the quotation request for Lab without creating another sample delivery request.", "Note", ["note"]);
        Add("Complete", "Hoàn thành yêu cầu", "Complete request", "Chốt VU được chọn để sử dụng khi lên đơn; không tạo đơn ngay.", "Confirm the selected formula for ordering; does not create an order.");
        Add("Cancel", "Hủy yêu cầu phối mẫu", "Cancel sample request", "Bắt buộc có lý do, giữ lịch sử.", "A reason is required; preserve history.", "Note", ["note"], danger: true);
        return a;
    }
}
