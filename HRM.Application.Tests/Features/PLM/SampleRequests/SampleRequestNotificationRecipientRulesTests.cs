using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestNotificationRecipientRulesTests
{
    [Fact]
    public void Resolve_DefaultRecipients_SendsMutedParticipantsAsSilentOnly()
    {
        var senderId = Guid.NewGuid();
        var notifiedEmployeeId = Guid.NewGuid();
        var mutedEmployeeId = Guid.NewGuid();

        var result = SampleRequestNotificationRecipientRules.Resolve(
            senderId,
            new[]
            {
                new SampleRequestConversationParticipant(senderId, false),
                new SampleRequestConversationParticipant(notifiedEmployeeId, false),
                new SampleRequestConversationParticipant(mutedEmployeeId, true)
            },
            null);

        Assert.Equal(new[] { notifiedEmployeeId }, result.TargetEmployeeIds);
        Assert.Equal(new[] { mutedEmployeeId }, result.SilentEmployeeIds);
    }

    [Fact]
    public void Resolve_RecipientOverride_CannotBypassMutedParticipant()
    {
        var senderId = Guid.NewGuid();
        var mutedEmployeeId = Guid.NewGuid();
        var explicitRecipientId = Guid.NewGuid();

        var result = SampleRequestNotificationRecipientRules.Resolve(
            senderId,
            new[]
            {
                new SampleRequestConversationParticipant(mutedEmployeeId, true),
                new SampleRequestConversationParticipant(explicitRecipientId, false)
            },
            new[] { mutedEmployeeId, explicitRecipientId });

        Assert.Equal(new[] { explicitRecipientId }, result.TargetEmployeeIds);
        Assert.Equal(new[] { mutedEmployeeId }, result.SilentEmployeeIds);
    }

    [Fact]
    public void Resolve_AfterParticipantUnmutes_AllowsLaterNotifications()
    {
        var senderId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        var result = SampleRequestNotificationRecipientRules.Resolve(
            senderId,
            new[] { new SampleRequestConversationParticipant(employeeId, false) },
            new[] { employeeId });

        Assert.Equal(new[] { employeeId }, result.TargetEmployeeIds);
        Assert.Empty(result.SilentEmployeeIds);
    }
}
