using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.ExportIqcExcel;

/// <summary>Xuất Excel IQC hai sheet chi tiết và báo cáo theo bộ lọc, yêu cầu quyền xem và company scope.</summary>
public sealed record ExportIqcExcelQuery(QCInputQuery Filter) : IRequest<OperationResult<IqcFileDto>>;
