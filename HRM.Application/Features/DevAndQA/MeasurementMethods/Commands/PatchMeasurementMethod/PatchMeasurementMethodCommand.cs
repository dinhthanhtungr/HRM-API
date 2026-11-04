using HRM.Application.Features.DevAndQA.MeasurementMethods.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.MeasurementMethods.Commands.PatchMeasurementMethod;

/// <summary>PatchMeasurementMethod: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record PatchMeasurementMethodCommand(Guid Id, string? Code = null, string? Name = null, string? Description = null, bool? IsActive = null, [property: System.Text.Json.Serialization.JsonIgnore] int? SortOrder = null, IReadOnlyList<string>? ClearFields = null, string? NameEn = null) : IRequest<MeasurementMethodDto>;
