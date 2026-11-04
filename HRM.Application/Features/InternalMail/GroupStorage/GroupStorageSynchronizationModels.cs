using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.InternalMail.GroupStorage;

// GROUP-STORAGE-TRANSITION: synchronization-api — remove the API/report contracts after acceptance, not the live writer.
public sealed record GroupStorageSyncFile(byte[] Content, string FileName, string Outcome, Guid RunId);

public interface IGroupStorageSynchronization
{
    Task<GroupStorageSyncFile> SynchronizeAsync(Guid companyId, CancellationToken ct, bool includePresidentMemberships = true);
}

public interface IGroupStorageSyncExcelRenderer
{
    byte[] Render(GroupStorageSyncReport report);
}

public sealed class GroupStorageSyncReport
{
    public Guid RunId { get; } = Guid.CreateVersion7();
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public Guid CompanyId { get; init; }
    public string Outcome { get; set; } = "Blocked";
    public bool IncludePresidentMemberships { get; init; } = true;
    public List<string> Issues { get; } = [];
    public List<GroupStorageSyncRow> Rows { get; } = [];
    public List<PresidentMembershipSyncRow> PresidentMemberships { get; } = [];
}

public sealed record GroupStorageSyncRow(Guid ConversationId, Guid RootConversationId, string RecordCode,
    string? ColourCode, InternalConversationGroupType GroupType, string Name, bool IsActive,
    string Result, int VisibleMessages, int DeletedMessages);

public enum PresidentMembershipSyncStatus { Added, Existing, Removed, Skipped, RolledBack, Unknown }

public sealed record PresidentMembershipSyncRow(Guid ConversationId, Guid RootConversationId, Guid EmployeeId,
    PresidentMembershipSyncStatus Status, string Reason);
