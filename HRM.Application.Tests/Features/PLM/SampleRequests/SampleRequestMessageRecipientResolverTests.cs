using HRM.Application.Features.PLM.SampleRequests.Services;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestMessageRecipientResolverTests
{
    [Fact]
    public void ResolveContextRequiredRecipientIds_IncludesParticipantsAndManagerButExcludesSender()
    {
        var senderId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var result = SampleRequestMessageRecipientResolver.ResolveContextRequiredRecipientIds(
            new[] { senderId, participantId },
            managerId,
            senderId);

        Assert.Contains(participantId, result);
        Assert.Contains(managerId, result);
        Assert.DoesNotContain(senderId, result);
    }

    [Fact]
    public void ResolveContextRequiredRecipientIds_RemovesEmptyAndDuplicateIds()
    {
        var participantId = Guid.NewGuid();

        var result = SampleRequestMessageRecipientResolver.ResolveContextRequiredRecipientIds(
            new[] { Guid.Empty, participantId, participantId },
            participantId,
            Guid.NewGuid());

        Assert.Equal(new[] { participantId }, result);
    }
}
