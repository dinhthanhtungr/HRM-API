using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.PatchIqc;

/// <summary>Cập nhật các field IQC được gửi và đồng bộ PO; yêu cầu quyền sửa, company và employee hợp lệ.</summary>
public sealed record PatchIqcCommand(long VoucherDetailId, PatchQCInputByQC Request) : IRequest<OperationResult<GetQCInputByQC>>;
