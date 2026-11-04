namespace HRM.Application.Features.PLM.SampleRequests.Dtos;

public sealed record SilentWatcherReconciliationRow(Guid EmployeeId, string Name, string Status);
public sealed record SilentWatcherReconciliationDto(string Outcome, string? SampleRequestCode,
    string? Fingerprint, IReadOnlyList<SilentWatcherReconciliationRow> Rows, int AddedCount = 0)
{
    public int MissingCount => Rows.Count(x => x.Status == "Missing");
}
