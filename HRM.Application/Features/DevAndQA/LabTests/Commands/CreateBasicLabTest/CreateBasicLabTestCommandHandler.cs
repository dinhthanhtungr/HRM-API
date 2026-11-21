using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Commons.Pagination;
using MediatR;
using HRM.Application.Features.DevAndQA.LabTests.Services.Basic;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.CreateBasicLabTest;

internal sealed class CreateBasicLabTestCommandHandler(BasicLabTestService service) : IRequestHandler<CreateBasicLabTestCommand, BasicLabTestDto>
{
    public Task<BasicLabTestDto> Handle(CreateBasicLabTestCommand request, CancellationToken ct) => service.SaveAsync(null, request.Data, ct);
}

