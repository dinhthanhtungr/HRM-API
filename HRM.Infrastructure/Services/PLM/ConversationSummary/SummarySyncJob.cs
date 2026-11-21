using System.Text.Json;
using HRM.Domain.Entities.Notifications;

namespace HRM.Infrastructure.Services.PLM.ConversationSummary;

internal sealed record SummarySyncJob(Guid CompanyId, Guid EntityId, string Kind, Guid? Cursor = null, int Version = 1)
{
    internal const string Type = "SampleRequestConversationSummarySync";
    internal OutboxMessage ToOutbox() => new() { Type = Type, PayloadJson = JsonSerializer.Serialize(this) };
}
