using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.DeleteLabTest;

internal sealed class DeleteLabTestCommandHandler(LabTestService service)
    : IRequestHandler<DeleteLabTestCommand, bool>
{
    public Task<bool> Handle(DeleteLabTestCommand request, CancellationToken ct) => service.DeleteAsync(request.Id, ct);
}
