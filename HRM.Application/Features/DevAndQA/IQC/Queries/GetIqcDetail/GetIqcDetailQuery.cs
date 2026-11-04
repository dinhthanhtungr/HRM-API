using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcDetail;

/// <summary>Lấy context dòng phiếu và QC mới nhất; trả context tạo mới khi dòng chưa có QC.</summary>
public sealed record GetIqcDetailQuery(long VoucherDetailId) : IRequest<OperationResult<GetQCInputByQC?>>;
