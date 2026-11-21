using System.Text.Json;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationDisplayStatusTests
{
    [Theory]
    [InlineData(QuotationStatus.Draft, ProductStandardPriceState.PendingInitialApproval, "Draft")]
    [InlineData(QuotationStatus.PendingApproval, ProductStandardPriceState.Active, "PendingPriceApproval")]
    [InlineData(QuotationStatus.Approved, ProductStandardPriceState.Active, "Approved")]
    [InlineData(QuotationStatus.Approved, ProductStandardPriceState.Missing, "PendingPriceApproval")]
    [InlineData(QuotationStatus.Approved, ProductStandardPriceState.PendingReapproval, "PendingPriceUpdate")]
    [InlineData(QuotationStatus.PendingSendConfirmation, ProductStandardPriceState.PendingReapproval, "PendingSendConfirmation")]
    [InlineData(QuotationStatus.Sent, ProductStandardPriceState.PendingReapproval, "Sent")]
    [InlineData(QuotationStatus.SendRejected, ProductStandardPriceState.PendingReapproval, "SendRejected")]
    public void DisplayPriorityMatchesContract(QuotationStatus status, ProductStandardPriceState review, string code)
        => Assert.Equal(code, QuotationDisplayStatusResolver.Resolve(status, [new() { State = review }]).Code);

    public static IEnumerable<object[]> States()
        => from status in Enum.GetValues<QuotationStatus>()
           from review in Enum.GetValues<ProductStandardPriceState>()
           select new object[] { status, review };

    [Theory]
    [MemberData(nameof(States))]
    public void ListAndDetailUseSameRuleWithoutChangingWorkflow(QuotationStatus status, ProductStandardPriceState state)
    {
        var review = new QuotationStandardPriceReviewDto { State = state };
        var list = new QuotationListItemDto { Status = status, StandardPriceReview = review };
        var detail = new QuotationDetailDto
        {
            Status = status,
            Lines = [new QuotationLineDto { StandardPriceReview = review }]
        };
        Assert.Equal(list.DisplayStatus, detail.DisplayStatus);
        Assert.Equal(status, list.Status);
        if (status is QuotationStatus.PendingApproval or QuotationStatus.Approved && state == ProductStandardPriceState.PendingReapproval)
            Assert.Equal("PendingPriceUpdate", list.DisplayStatus.Code);
        if (status is QuotationStatus.Sent or QuotationStatus.PendingSendConfirmation or QuotationStatus.Cancelled)
            Assert.Equal(status.ToString(), list.DisplayStatus.Code);
    }

    [Fact]
    public void NewSaleRequestOverridesExistingApprovedPriceUntilApprovalCatchesUp()
    {
        var approvedAt = new DateTime(2026, 11, 1);
        var active = new ProductStandardPriceStateResult(ProductStandardPriceState.Active, false, false, null, approvedAt);
        Assert.Equal(ProductStandardPriceState.PendingReapproval,
            ProductStandardPriceStateResolver.ApplySaleRequest(active, approvedAt.AddMinutes(1)).State);
        Assert.Equal(ProductStandardPriceState.Active,
            ProductStandardPriceStateResolver.ApplySaleRequest(active, approvedAt).State);
        Assert.Equal(ProductStandardPriceState.Active,
            ProductStandardPriceStateResolver.ApplySaleRequest(active, null).State);
    }

    [Fact]
    public void PendingReapprovalWinsAcrossReturnedActiveLines()
    {
        var detail = new QuotationDetailDto
        {
            Status = QuotationStatus.Approved,
            Lines = [
                new QuotationLineDto { StandardPriceReview = new() { State = ProductStandardPriceState.Active } }]
        };
        Assert.Equal("Approved", detail.DisplayStatus.Code);
        var mixed = QuotationDisplayStatusResolver.Resolve(QuotationStatus.Approved,
            [new() { State = ProductStandardPriceState.PendingInitialApproval },
             new() { State = ProductStandardPriceState.PendingReapproval }]);
        Assert.Equal("PendingPriceUpdate", mixed.Code);
    }

    [Fact]
    public void SerializationAddsDisplayContractAndKeepsNumericStatus()
    {
        var dto = new QuotationListItemDto
        {
            Status = QuotationStatus.Approved,
            StandardPriceReview = new() { State = ProductStandardPriceState.PendingReapproval }
        };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(20, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("PendingPriceUpdate", json.RootElement.GetProperty("displayStatus").GetProperty("code").GetString());
        Assert.Equal("warning", json.RootElement.GetProperty("displayStatus").GetProperty("tone").GetString());
    }
}
