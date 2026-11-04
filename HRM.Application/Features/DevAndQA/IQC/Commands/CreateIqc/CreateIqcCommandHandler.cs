using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.CreateIqc;

/// <summary>Tạo IQC cho dòng phiếu kho; POST lặp trả QC đã lưu và đồng bộ lại PO theo quyết định hiện có.</summary>
internal sealed class CreateIqcCommandHandler(IqcWriter writer)
    : IRequestHandler<CreateIqcCommand, OperationResult<GetQCInputByQC>>
{
    public Task<OperationResult<GetQCInputByQC>> Handle(CreateIqcCommand command, CancellationToken ct) =>
        writer.CreateAsync(command.Request, ct);
}
