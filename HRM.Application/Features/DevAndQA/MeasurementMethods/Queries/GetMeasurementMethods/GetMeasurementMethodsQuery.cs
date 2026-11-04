using HRM.Application.Features.DevAndQA.MeasurementMethods.Dtos;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using MediatR;

namespace HRM.Application.Features.DevAndQA.MeasurementMethods.Queries.GetMeasurementMethods;

/// <summary>GetMeasurementMethods: dữ liệu đánh giá động, được scope công ty và quyền phía service.</summary>
public sealed record GetMeasurementMethodsQuery(string? Keyword = null, bool? IsActive = null, int PageNumber = 1, int PageSize = 50) : IRequest<PagedResult<MeasurementMethodDto>>;
