using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.CreateLabTest;

internal sealed class CreateLabTestCommandHandler(LabTestService service)
    : IRequestHandler<CreateLabTestCommand, LabTestDto>
{
    public Task<LabTestDto> Handle(CreateLabTestCommand request, CancellationToken ct) => service.CreateAsync(request.Data, ct);
}
