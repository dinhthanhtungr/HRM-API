using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.PatchLabTest;

internal sealed class PatchLabTestCommandHandler(LabTestService service)
    : IRequestHandler<PatchLabTestCommand, LabTestDto>
{
    public Task<LabTestDto> Handle(PatchLabTestCommand request, CancellationToken ct) => service.PatchAsync(request.Id, request.Data, ct);
}
