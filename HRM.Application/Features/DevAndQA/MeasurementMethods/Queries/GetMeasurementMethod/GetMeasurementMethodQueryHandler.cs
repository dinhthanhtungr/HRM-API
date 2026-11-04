using HRM.Application.Features.DevAndQA.MeasurementMethods.Dtos;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.MeasurementMethods.Queries.GetMeasurementMethod;

internal sealed class GetMeasurementMethodQueryHandler(MeasurementMethodService service) : IRequestHandler<GetMeasurementMethodQuery, MeasurementMethodDto>
{
    public Task<MeasurementMethodDto> Handle(GetMeasurementMethodQuery request, CancellationToken ct) => service.GetMethodAsync(request.Id, ct);
}
