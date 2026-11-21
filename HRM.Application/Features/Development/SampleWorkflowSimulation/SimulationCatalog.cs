namespace HRM.Application.Features.Development.SampleWorkflowSimulation;

/// <summary>Synthetic proposal fixtures only. Add scenarios here; never query or seed application databases.</summary>
public static class SimulationCatalog
{
    public static SimulationQuotationContext Quotation(string id, SimulationState state)
    {
        var context = new SimulationQuotationContext { QuotedAt = state.QuotationFeedback?.Date ?? (state.PriceAccepted || state.PriceRejected ? "2026-11-10" : null),
            AcceptedAt = state.PriceAccepted ? state.QuotationFeedback?.FeedbackDate ?? "2026-11-14" : null,
            PreviousOutcome = state.QuotationFeedback?.Outcome ?? (state.PriceRejected ? "Rejected" : null),
            PreviousDate = state.QuotationFeedback?.FeedbackDate ?? "2026-11-14", PreviousNote = state.QuotationFeedback?.Note,
            PriceReference = state.QuotationFeedback?.Reference };
        return id switch
        {
            "price-linked" => context with { QuotedAt = state.QuotationFeedback?.Date ?? "2026-11-10", PriceReferences = [new("sim-line-1", "BBG_SIM_001 · WP31070C", "2026-11-10"), new("sim-line-2", "BBG_SIM_002 · WP31070C", "2026-11-12")] },
            "price-locked" => context with { QuotedAt = "2026-11-10", DateEditable = false, AcceptedAt = "2026-11-14", PriceReference = "BBG_SIM_001" },
            "price-readonly" => context with { QuotedAt = "2026-11-10", DateEditable = false, CanConfirmCustomerPrice = false },
            "price-date-only" => context with { QuotedAt = state.QuotationFeedback?.Date ?? "2026-11-10", CanConfirmCustomerPrice = false },
            "price-requoted" => context with { QuotedAt = state.QuotationFeedback?.Date ?? "2026-11-10", PreviousOutcome = state.QuotationFeedback?.Outcome ?? "Rejected", PreviousDate = state.QuotationFeedback?.FeedbackDate ?? "2026-11-11", PreviousNote = state.QuotationFeedback?.Note ?? "Giá cũ chưa phù hợp / Old price rejected", ApprovedAt = "2026-11-13" },
            "price-hidden" => context with { QuotedAt = state.QuotationFeedback?.Date ?? "2026-11-10", ContentHidden = true, NoteHidden = true, PreviousOutcome = "Rejected", PreviousNote = null, PriceReference = null },
            _ => context
        };
    }
    public static SimulationText Text(string vi, string en) => new(vi, en);
    public static IReadOnlyList<SimulationOption> Candidates() => Enumerable.Range(1, 3)
        .Select(i => new SimulationOption(i.ToString(), $"VU26110000{i}", i == 3 ? 3 : 2, Confirmed: i != 3)).ToArray();
    private static SimulationBatch Batch(int number, string result = "") => new(number, "2026-11-14",
        Enumerable.Range(1, 3).Select(i => new SimulationOption($"option-{number}-{i}", $"VU26110000{i}", i == 3 ? 3 : 2,
            result == "Approved" ? i == 2 ? "Approved" : "Failed" : result,
            result == "Approved" ? i == 2 ? "Khách chọn Option 2 / Customer selected Option 2" : "Không được chọn / Not selected" : "",
            result.Length > 0 ? "2026-11-14" : null)).ToArray());
    // Return fresh records/collections for every request. No session state or shared mutable fixtures.
    public static IReadOnlyList<SimulationScenario> All() => BusinessSimulationCatalog.All();
    internal static SimulationScenario? Find(string id) => All().Concat(RegressionFixtures()).SingleOrDefault(s => s.Id == id);
    // Retained for automated permission/form regressions, never listed in the business picker.
    public static IReadOnlyList<SimulationScenario> RegressionFixtures() =>
    [
        new("parallel", Text("Chờ mẫu mới · thiếu giá và phản hồi đợt cũ", "New sample pending · price and previous feedback missing"), new() { SampleKg = 10, PricePending = true, HasHistory = true, Batches = [Batch(1)] }),
        new("price-linked", Text("Giá · có hai báo giá liên kết", "Price · two linked quotations"), new() { PricePending = true }),
        new("price-locked", Text("Giá · khách đã đồng ý, khóa phản hồi và ngày", "Price · accepted, response and date locked"), new() { PriceAccepted = true }),
        new("price-readonly", Text("Giá · chỉ được xem", "Price · read only"), new() { PricePending = true }),
        new("price-date-only", Text("Giá · chỉ được sửa ngày báo giá", "Price · edit quotation date only"), new() { PricePending = true }),
        new("price-requoted", Text("Giá · đã duyệt lại sau khi khách từ chối", "Price · approved again after rejection"), new() { PricePending = true }),
        new("price-hidden", Text("Giá · đã chấp nhận, nội dung bị giới hạn", "Price · accepted, restricted content"), new() { PriceAccepted = true }),
        new("empty", Text("Chưa yêu cầu chip hoặc mẫu", "No chip or sample requested"), new() { PricePending = true }),
        new("chip", Text("Đã yêu cầu chip · Lab chưa gửi", "Chip requested · not sent"), new() { ChipSets = 3, PricePending = true }),
        new("kg", Text("Đã yêu cầu 10 kg · Lab chưa gửi", "10 kg requested · not sent"), new() { SampleKg = 10, PricePending = true }),
        new("both", Text("Chip rồi kg · chưa gửi chip", "Chip then kg · chip not sent"), new() { ChipSets = 3, SampleKg = 10, Combined = true, PricePending = true }),
        new("quote-pending", Text("Chờ chuẩn bị báo giá", "Quotation preparation pending"), new() { QuotePending = true, FormulaReady = false }),
        new("all-pending", Text("Chờ giá, chip, kg · còn phản hồi cũ", "Quotation, chip, kg pending · old feedback missing"), new() { QuotePending = true, ChipSets = 3, SampleKg = 10, Combined = true, HasHistory = true, Batches = [Batch(1)] }),
        new("after-chip", Text("Chip đã gửi · kg đang chờ", "Chip sent · kg pending"), new() { SampleKg = 10, Combined = true, ChipSent = true, HasHistory = true, PricePending = true }),
        new("feedback", Text("Mẫu đã gửi · chưa phản hồi", "Samples sent · feedback pending"), new() { HasHistory = true, PricePending = true, Batches = [Batch(1)] }),
        new("partial", Text("Một option không đạt · còn option chờ", "One failed option · others pending"), new() { HasHistory = true, PricePending = true, Batches = [Batch(1) with { Options = Batch(1).Options.Select((o, i) => i == 0 ? o with { Result = "Failed", ReplyDate = "2026-11-14" } : o).ToArray() }] }),
        new("failed", Text("Cả đợt không đạt", "All options failed"), new() { HasHistory = true, PriceAccepted = true, Batches = [Batch(1, "Failed")] }),
        new("price-left", Text("Mẫu đạt · còn thiếu phản hồi giá", "Sample accepted · price feedback missing"), new() { HasHistory = true, PricePending = true, Batches = [Batch(1, "Approved")] }),
        new("ready", Text("Giá và mẫu đạt · đủ điều kiện hoàn thành", "Price and sample accepted · ready to complete"), new() { HasHistory = true, PriceAccepted = true, Batches = [Batch(1, "Approved")] }),
        new("old-batches", Text("Nhiều đợt cũ · quy tắc chưa chốt", "Multiple old batches · policy unresolved"), new() { HasHistory = true, PriceAccepted = true, Batches = [Batch(1), Batch(2, "Approved")], UnresolvedRule = "OldBatchPolicy" }),
        new("completed", Text("TP đã hoàn thành · bổ sung", "Completed request · supplementary work"), new() { HasHistory = true, PriceAccepted = true, Batches = [Batch(1, "Approved")], Status = "Completed" }),
        new("quote-ready", Text("Báo giá đủ điều kiện · chưa gửi", "Quotation ready · not sent"), new() { QuoteReady = true }),
        new("price-rejected", Text("Khách chưa đồng ý giá", "Customer rejected the price"), new() { PriceRejected = true, HasHistory = true }),
        new("draft", Text("Chờ mẫu · VU chưa đủ điều kiện", "Sample pending · formula not ready"), new() { SampleKg = 10, FormulaReady = false, PricePending = true }),
        new("cancelled", Text("TP đã hủy", "Cancelled request"), new() { Status = "Cancelled", HasHistory = true }),
        new("review", Text("Dữ liệu cũ thiếu bằng chứng", "Legacy data requiring review"), new() { Status = "Review", UnresolvedRule = "LegacyEvidence" })
    ];
}
