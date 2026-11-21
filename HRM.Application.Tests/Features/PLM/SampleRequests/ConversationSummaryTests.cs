using System.Globalization;
using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using HRM.Domain.Entities.SampleRequestSchema;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class ConversationSummaryTests
{
    [Fact]
    public void FingerprintTracksDisplayedContentButExcludesPrivateDataAndAuditTimestamps()
    {
        var sample = new SampleRequest { ExternalId = "SYNTHETIC", SaleComment = "Old", ExpectedPrice = 123456, UpdatedDate = DateTime.Now };
        var product = new Product { ColourCode = "TEST", LabComment = "PRIVATE-LAB-NOTE" };
        string Content() => ConversationSummaryContent.Serialize(ConversationSummaryContent.Build(sample, product));
        var original = Content();
        Assert.DoesNotContain("123456", original); Assert.DoesNotContain("PRIVATE-LAB-NOTE", original);
        sample.ExpectedPrice = 999; sample.UpdatedDate = DateTime.Now.AddDays(1); product.LabComment = "DIFFERENT";
        Assert.Equal(original, Content());
        sample.SaleComment = "New";
        Assert.NotEqual(ConversationSummaryContent.Fingerprint(original), ConversationSummaryContent.Fingerprint(Content()));
        original = Content(); product.ColourCode = "TEST2";
        Assert.NotEqual(original, Content());
    }

    [Theory]
    [InlineData("private", "NORMAL")]
    [InlineData("Nội bộ", "NORMAL")]
    [InlineData("Báo giá", "KH_VIETAUS")]
    public void InternalRequestsAreSuppressed(string type, string customer) => Assert.True(ConversationSummaryContent.Suppressed(type, customer));

    [Fact]
    public void CanonicalFormattingDoesNotDependOnServerCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var sample = new SampleRequest { ExpectedQuantity = 1.25, ExpectedDeliveryDate = new DateTime(2026, 11, 7) };
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            var first = ConversationSummaryContent.Serialize(ConversationSummaryContent.Build(sample, new Product()));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(first, ConversationSummaryContent.Serialize(ConversationSummaryContent.Build(sample, new Product())));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
