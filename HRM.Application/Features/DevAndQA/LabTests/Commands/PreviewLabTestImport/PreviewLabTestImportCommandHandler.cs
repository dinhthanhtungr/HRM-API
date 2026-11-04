using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Services;
using MediatR;
using HRM.Application.Abstractions.Documents;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.PreviewLabTestImport;

internal sealed class PreviewLabTestImportCommandHandler(LabTestService service, ILabTestSheetReader reader)
    : IRequestHandler<PreviewLabTestImportCommand, LabTestImportPreviewDto>
{
    public Task<LabTestImportPreviewDto> Handle(PreviewLabTestImportCommand request, CancellationToken ct) => service.PreviewImportAsync(request.Content, request.FileName, request.ColumnStandards, reader, ct);
}
