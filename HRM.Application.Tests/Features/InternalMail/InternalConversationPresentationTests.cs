using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalConversationPresentationTests
{
    [Theory]
    [InlineData("Trao đổi yêu cầu phối mẫu TP_29583 - RH31018", "TP_29583 - RH31018")]
    [InlineData("Trao doi yeu cau phoi mau TP_29583 - RH31018", "TP_29583 - RH31018")]
    [InlineData("TP_29583 - RH31018", "TP_29583 - RH31018")]
    public void SampleRequestDisplayTitle_RemovesOnlyKnownLegacyPrefix(
        string subject,
        string expected)
    {
        var result = InternalConversationPresentation.BuildDisplayTitle(
            InternalMailRelatedType.SampleRequest,
            subject,
            "TP_29583");

        Assert.Equal(expected, result);
    }

    [Fact]
    public void OtherConversation_KeepsItsSubject()
    {
        var result = InternalConversationPresentation.BuildDisplayTitle(
            InternalMailRelatedType.Quotation,
            "Báo giá BBG260700001",
            "BBG260700001");

        Assert.Equal("Báo giá BBG260700001", result);
    }

    [Fact]
    public void EmptySubject_FallsBackToRelatedExternalId()
    {
        var result = InternalConversationPresentation.BuildDisplayTitle(
            InternalMailRelatedType.SampleRequest,
            "  ",
            "TP_29583");

        Assert.Equal("TP_29583", result);
    }
}
