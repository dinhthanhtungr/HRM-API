using HRM.Application.Features.PLM.SampleRequests.Dtos;

namespace HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;

public sealed class ConversationSummaryOptions
{
    public const string SectionName = "SampleRequests:ConversationSummary";
    // Enable only AFTER the approved projection table exists. No schema probes on requests.
    public bool Enabled { get; set; }
}

public interface ISampleRequestConversationSummaryStore
{
    Task<ConversationSummaryDto> ReadAsync(Guid company, Guid sampleRequest, CancellationToken ct);
    Task<SummaryBackfillPage> EnqueuePageAsync(Guid company, Guid? cursor, int take, Guid? sampleRequest, CancellationToken ct);
    Task<bool> ProcessOneAsync(CancellationToken ct);
    Task<SummarySyncStatus> StatusAsync(Guid company, CancellationToken ct);
}
