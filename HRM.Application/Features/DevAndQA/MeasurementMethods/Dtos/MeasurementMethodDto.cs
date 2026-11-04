using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

namespace HRM.Application.Features.DevAndQA.MeasurementMethods.Dtos;

public sealed record MeasurementMethodDto(Guid Id, string Code, string Name, string? Description,
    bool IsActive, int SortOrder, DateTime CreatedDate, Guid CreatedBy, DateTime? UpdatedDate, Guid? UpdatedBy, string? NameEn = null);
