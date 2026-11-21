using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Commons.Pagination;
using MediatR;
using HRM.Application.Features.DevAndQA.LabTests.Services.Basic;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.PatchBasicLabTest;

internal sealed class PatchBasicLabTestCommandHandler(BasicLabTestService service) : IRequestHandler<PatchBasicLabTestCommand, BasicLabTestDto>
{
    public Task<BasicLabTestDto> Handle(PatchBasicLabTestCommand request, CancellationToken ct) => service.SaveAsync(request.Id, request.Data, ct);
}

