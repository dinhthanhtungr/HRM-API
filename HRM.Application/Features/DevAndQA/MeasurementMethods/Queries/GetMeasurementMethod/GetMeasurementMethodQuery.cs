using HRM.Application.Features.DevAndQA.MeasurementMethods.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.MeasurementMethods.Queries.GetMeasurementMethod;

/// <summary>GetMeasurementMethod: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record GetMeasurementMethodQuery(Guid Id) : IRequest<MeasurementMethodDto>;
