namespace HRM.Application.Features.PLM.SampleRequests.Dtos;

public sealed record SummaryField(string Code, string Value);
public sealed record ConversationSummaryDto(string Status, DateTime? SyncedAt, IReadOnlyList<SummaryField> Fields);
public sealed record SummaryBackfillPage(int Queued, Guid? NextCursor, bool HasMore, bool Enabled = true);
public sealed record SummarySyncStatus(bool Enabled, int Pending, int Failed, int Completed);
