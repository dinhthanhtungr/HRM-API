using HRM.Application.Features.DevAndQA.MeasurementMethods.Dtos;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.MeasurementMethods.Commands.PatchMeasurementMethod;

internal sealed class PatchMeasurementMethodCommandHandler(MeasurementMethodService service) : IRequestHandler<PatchMeasurementMethodCommand, MeasurementMethodDto>
{
    public Task<MeasurementMethodDto> Handle(PatchMeasurementMethodCommand request, CancellationToken ct) => service.PatchMethodAsync(request, ct);
}
