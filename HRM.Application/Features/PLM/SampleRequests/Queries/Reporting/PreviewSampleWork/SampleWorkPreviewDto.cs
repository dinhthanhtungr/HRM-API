using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.PreviewSampleWork;

public sealed record SampleWorkPreviewDto(string Persona, Guid EmployeeId, bool ReadOnly,
    PagedResult<SampleRequestSampleTrialReportDto> Report);
public sealed record SampleWorkPreviewEmployeeDto(Guid EmployeeId, string Name, string? ExternalId);
public sealed record SampleWorkPreviewOptionsDto(IReadOnlyList<SampleWorkPreviewEmployeeDto> Employees, string[] Personas);
