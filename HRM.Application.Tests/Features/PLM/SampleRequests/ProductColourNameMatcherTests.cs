using HRM.Application.Features.PLM.SampleRequests.Commands.FillProductColourNames;
using HRM.Domain.ReferenceData.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class ProductColourNameMatcherTests
{
    [Theory]
    [InlineData("HẠT MÀU ĐEN - BLACK MB", "Black")]
    [InlineData("BỘT MÀU ĐỎ RED PIGMENT", "Red")]
    [InlineData("HẠT MÀU XANH BLUE PANTONE 7467C", "Blue")]
    [InlineData("HẠT MÀU XANH LÁ", "Green")]
    [InlineData("BỘT MÀU TÍM", "Violet")]
    [InlineData("GREY MB", "Grey")]
    [InlineData("HẠT MÀU XANH", null)]
    [InlineData("RED BLUE", null)]
    [InlineData("RED123", null)]
    [InlineData("BỘT MÀU", null)]
    [InlineData(null, null)]
    public void MatchesOnlyUnambiguousCanonicalColour(string? name, string? expected)
    {
        var result = ProductColourNameMatcher.Match(name);
        Assert.Equal(expected, result);
        if (result != null) Assert.Contains(SampleRequestReferenceData.Colors, x => x.Value == result);
    }
}
