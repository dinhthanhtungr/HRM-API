using System.Text.Json;
using HRM.Application.Features.InternalMail.Services;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalMailMentionsTests
{
    [Fact]
    public void EditingLegacyMessageDoesNotChangeItsUnreadPolicy()
    {
        var payload = InternalMailMentions.Write("{\"contentType\":\"InternalMailMessage\"}", [], markAudience: false);
        using var document = JsonDocument.Parse(payload);
        Assert.False(document.RootElement.TryGetProperty("notificationAudienceVersion", out _));
    }

    [Fact]
    public void EditingMentionsPreservesWorkflowPayloadAndPreviouslyNotifiedEmployees()
    {
        var previous = Guid.NewGuid();
        var added = Guid.NewGuid();
        var original = "{\"contentType\":\"SampleRequestDataChangeRequest\",\"dataChangeRequest\":{\"status\":\"pending\"}}";
        var payload = InternalMailMentions.Write(original, [new(added, "Lab Nam")], [previous]);
        using var document = JsonDocument.Parse(payload);
        Assert.Equal("pending", document.RootElement.GetProperty("dataChangeRequest").GetProperty("status").GetString());
        Assert.Equal(new[] { previous, added }, InternalMailMentions.Notified(payload));
        Assert.Equal(new InternalMailMention(added, "Lab Nam"), Assert.Single(InternalMailMentions.Read(payload)));
        var removed = InternalMailMentions.Write(payload, [], InternalMailMentions.Notified(payload));
        Assert.Empty(InternalMailMentions.Read(removed));
        Assert.Contains(added, InternalMailMentions.Notified(removed));
    }

    [Fact]
    public void DeliveryBypassUsesOnlyServerDeliveryMetadataNotAllVisibleMentions()
    {
        var tagged = Guid.NewGuid();
        var oldPayload = InternalMailMentions.Write(null, [new(tagged, "Lab Nam")]);
        Assert.Empty(InternalMailMentions.DeliveryMentions(oldPayload));
        var payload = JsonSerializer.Serialize(new { deliveryMentionEmployeeIds = new[] { tagged } });
        Assert.Equal(tagged, Assert.Single(InternalMailMentions.DeliveryMentions(payload)));
        Assert.Empty(InternalMailMentions.Read("invalid historical payload"));
        Assert.Empty(InternalMailMentions.Read("[]"));
    }
}
