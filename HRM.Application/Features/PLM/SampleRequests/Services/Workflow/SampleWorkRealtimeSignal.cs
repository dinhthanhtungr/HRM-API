namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

/// <summary>Company-scoped invalidation only. No record IDs, prices or business text are broadcast.</summary>
public static class SampleWorkRealtimeSignal
{
    public const string OutboxType = "SampleWorkChanged";
    public const string EventName = "sampleWorkChanged";
}
