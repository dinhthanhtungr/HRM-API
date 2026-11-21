namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;

public sealed record LabNoteDto(Guid Id, string Note, DateTime RecordedAt, string? RecordedBy);
public sealed record LabNotesPage(IReadOnlyList<LabNoteDto> Items, bool HasMore, bool CanWrite, DateTime? ExpectedUpdatedDate);
