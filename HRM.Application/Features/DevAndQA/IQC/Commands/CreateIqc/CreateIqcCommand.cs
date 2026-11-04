using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.CreateIqc;

/// <summary>Tạo IQC cho dòng phiếu kho; POST lặp trả QC đã lưu và đồng bộ lại PO theo quyết định hiện có.</summary>
public sealed record CreateIqcCommand(PostQCInputByQC Request) : IRequest<OperationResult<GetQCInputByQC>>;
