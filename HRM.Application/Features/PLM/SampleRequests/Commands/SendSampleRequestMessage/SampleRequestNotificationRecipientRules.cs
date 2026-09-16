namespace HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;

/// <summary>
/// Quyết định recipient của notification phát sinh từ Sample Request conversation.
/// Participant mute vẫn giữ quyền xem thread/Hub nhưng không nhận badge, SignalR hay Web Push,
/// kể cả khi một action truyền danh sách recipient override.
/// </summary>
internal static class SampleRequestNotificationRecipientRules
{
    public static SampleRequestNotificationRecipients Resolve(
        Guid senderEmployeeId,
        IReadOnlyCollection<SampleRequestConversationParticipant> participants,
        IReadOnlyCollection<Guid>? recipientEmployeeIdsOverride)
    {
        var activeParticipants = participants
            .Where(participant => participant.EmployeeId != Guid.Empty && participant.EmployeeId != senderEmployeeId)
            .GroupBy(participant => participant.EmployeeId)
            .Select(group => group.First())
            .ToArray();
        var mutedEmployeeIds = activeParticipants
            .Where(participant => participant.IsMuted)
            .Select(participant => participant.EmployeeId)
            .ToHashSet();

        var candidateEmployeeIds = recipientEmployeeIdsOverride is null
            ? activeParticipants.Select(participant => participant.EmployeeId)
            : recipientEmployeeIdsOverride;

        return new SampleRequestNotificationRecipients(
            candidateEmployeeIds
                .Where(employeeId => employeeId != Guid.Empty && employeeId != senderEmployeeId)
                .Where(employeeId => !mutedEmployeeIds.Contains(employeeId))
                .Distinct()
                .ToArray(),
            activeParticipants
                .Where(participant => participant.IsMuted)
                .Select(participant => participant.EmployeeId)
                .ToArray());
    }
}

internal sealed record SampleRequestConversationParticipant(Guid EmployeeId, bool IsMuted);

internal sealed record SampleRequestNotificationRecipients(
    IReadOnlyCollection<Guid> TargetEmployeeIds,
    IReadOnlyCollection<Guid> SilentEmployeeIds);
