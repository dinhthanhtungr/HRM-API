using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.PatchIqc;

/// <summary>Cập nhật các field IQC được gửi và đồng bộ PO; yêu cầu quyền sửa, company và employee hợp lệ.</summary>
internal sealed class PatchIqcCommandHandler(IqcWriter writer)
    : IRequestHandler<PatchIqcCommand, OperationResult<GetQCInputByQC>>
{
    public Task<OperationResult<GetQCInputByQC>> Handle(PatchIqcCommand command, CancellationToken ct) =>
        writer.PatchAsync(command.VoucherDetailId, command.Request, ct);
}
