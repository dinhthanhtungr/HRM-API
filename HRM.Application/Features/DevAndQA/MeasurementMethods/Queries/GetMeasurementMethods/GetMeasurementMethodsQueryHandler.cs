using HRM.Application.Features.DevAndQA.MeasurementMethods.Dtos;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.MeasurementMethods.Queries.GetMeasurementMethods;

internal sealed class GetMeasurementMethodsQueryHandler(MeasurementMethodService service) : IRequestHandler<GetMeasurementMethodsQuery, PagedResult<MeasurementMethodDto>>
{
    public Task<PagedResult<MeasurementMethodDto>> Handle(GetMeasurementMethodsQuery request, CancellationToken ct) => service.ListMethodsAsync(request.Keyword, request.IsActive, request.PageNumber, request.PageSize, ct);
}
