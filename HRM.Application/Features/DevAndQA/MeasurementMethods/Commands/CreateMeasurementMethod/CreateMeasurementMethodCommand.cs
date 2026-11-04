using HRM.Application.Features.DevAndQA.MeasurementMethods.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.MeasurementMethods.Commands.CreateMeasurementMethod;

/// <summary>CreateMeasurementMethod: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record CreateMeasurementMethodCommand(string Code, string Name, string? Description = null, [property: System.Text.Json.Serialization.JsonIgnore] int SortOrder = 0, string? NameEn = null) : IRequest<MeasurementMethodDto>;
