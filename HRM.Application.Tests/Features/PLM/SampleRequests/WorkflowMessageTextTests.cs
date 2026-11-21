using HRM.Application.Features.PLM.SampleRequests.Commands.DispatchWorkflowNotification;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class WorkflowMessageTextTests
{
    [Fact]
    public void LabQuotationCompletionDoesNotAppendSaleNextTask()
    {
        var text = WorkflowMessageText.Build(new() { EventType = SampleFlowRules.QuotationFinalized },
            null, "Lab đã hoàn thành công thức báo giá", "VU-1",
            WorkflowMessageText.Next("AwaitingSaleDecision"), false, null, null);
        Assert.Contains("Lab đã hoàn thành công thức báo giá", text.Body);
        Assert.Contains("Công thức: VU-1", text.Body);
        Assert.DoesNotContain("Tiếp theo:", text.Body);
    }

    [Theory]
    [InlineData("CustomerPriceAccepted", "Khách đồng ý giá")]
    [InlineData("CustomerPriceRejected", "Khách không đồng ý giá")]
    [InlineData("CustomerPriceNoResponse", "Khách chưa phản hồi giá")]
    public void GeneralFeedbackUsesParentOutcomeButNeverPrivateParentContent(string type, string title)
    {
        var parent = new SampleRequestWorkflowEvent { EventType = type, Note = "SECRET_NOTE", PriceReference = "SECRET_REFERENCE" };
        var evt = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.PriceFeedbackShared, Note = "Khách đề nghị làm mẫu" };
        var text = WorkflowMessageText.Build(evt, parent, "fallback", "VU-1", "Lab chuẩn bị mẫu.", false, "SECRET_OVERRIDE", null);
        Assert.Equal(title, text.Title);
        Assert.Contains("Nội dung gửi Lab: Khách đề nghị làm mẫu", text.Body);
        Assert.Contains("Công thức: VU-1", text.Body);
        Assert.DoesNotContain("Tiếp theo:", text.Body);
        Assert.DoesNotContain("SECRET", text.Body);
    }

    [Fact]
    public void NoteCorrectionDoesNotPretendToBeANewCustomerResponseOrPublishANextTask()
    {
        var text = WorkflowMessageText.Build(new() { EventType = SampleFlowRules.PriceFeedbackShared, Note = "Bổ sung" },
            new() { EventType = "CustomerPriceAccepted" }, "fallback", null, "Lab làm mẫu", true, null, null);
        Assert.Equal("Sale cập nhật nội dung phản hồi khách", text.Title);
        Assert.Contains("Kết quả hiện tại: Khách đồng ý giá", text.Body);
        Assert.DoesNotContain("Tiếp theo", text.Body);
    }

    [Fact]
    public void PricingMessageContainsOnlyItsOwnNotesAndReference()
    {
        var text = WorkflowMessageText.Build(new() { EventType = "CustomerPriceAccepted", Note = "Giữ giá đơn đầu", PriceReference = "BBG-1 · v2" },
            null, "fallback", "VU-1", "Sale theo dõi khách", false, null, null);
        Assert.Contains("Trao đổi giá: Giữ giá đơn đầu", text.Body);
        Assert.Contains("Báo giá: BBG-1 · v2", text.Body);
        Assert.DoesNotContain("Tiếp theo", text.Body);
    }

    [Theory]
    [InlineData(SampleFlowRules.QuotationFinalized)]
    [InlineData(SampleFlowRules.CompletedWithoutTrial)]
    public void CompletionIncludesFormulaButOmitsEmptyNotesAndUnknownNextSteps(string type)
    {
        var text = WorkflowMessageText.Build(new() { EventType = type }, null, "Hoàn thành", "VU-2", null, false, null, null);
        Assert.Contains("Công thức: VU-2", text.Body);
        Assert.DoesNotContain("Ghi chú:", text.Body);
        Assert.DoesNotContain("Tiếp theo:", text.Body);
        Assert.DoesNotContain("—", text.Body);
    }

    [Fact]
    public void NextStepUsesOnlyUnambiguousWorkflowStages()
    {
        Assert.Contains("Sale", WorkflowMessageText.Next("QuotationAccepted"));
        Assert.Contains("Lab", WorkflowMessageText.Next("FinalizeQuotationFormula"));
        Assert.Null(WorkflowMessageText.Next("QuotationPreparation"));
        Assert.Null(WorkflowMessageText.Next("Closed"));
    }
}
