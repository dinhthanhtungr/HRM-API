using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcSummary;

/// <summary>Lấy danh sách IQC phân trang theo bộ lọc trong company và quyền xem hiện tại.</summary>
public sealed record GetIqcSummaryQuery(QCInputQuery Filter) : IRequest<OperationResult<IqcPagedResult<GetSummaryQCInput>>>;
