using HRM.Application.Features.DevAndQA.MeasurementMethods.Dtos;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.MeasurementMethods.Commands.CreateMeasurementMethod;

internal sealed class CreateMeasurementMethodCommandHandler(MeasurementMethodService service) : IRequestHandler<CreateMeasurementMethodCommand, MeasurementMethodDto>
{
    public Task<MeasurementMethodDto> Handle(CreateMeasurementMethodCommand request, CancellationToken ct) => service.CreateMethodAsync(request, ct);
}
