using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.ImportLabTests;

internal sealed class ImportLabTestsCommandHandler(LabTestService service)
    : IRequestHandler<ImportLabTestsCommand, LabTestImportResultDto>
{
    public Task<LabTestImportResultDto> Handle(ImportLabTestsCommand request, CancellationToken ct) => service.ImportAsync(request.ImportBatchId, request.Rows, ct);
}
