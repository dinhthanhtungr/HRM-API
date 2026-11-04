using HRM.Application.Commons.Authorization;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using MediatR;
using HRM.Application.Abstractions.Documents;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.ExportIqcExcel;

/// <summary>Xuất Excel IQC hai sheet chi tiết và báo cáo theo bộ lọc, yêu cầu quyền xem và company scope.</summary>
internal sealed class ExportIqcExcelQueryHandler(IIqcDbContext context, ICurrentUser currentUser, IIqcExcelRenderer renderer, ICurrentUserPermissionService permissions)
    : IRequestHandler<ExportIqcExcelQuery, OperationResult<IqcFileDto>>
{
    public async Task<OperationResult<IqcFileDto>> Handle(ExportIqcExcelQuery query, CancellationToken ct)
    {
        if (!permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewIqc))
            return OperationResult<IqcFileDto>.Fail("Bạn không có quyền xem IQC.");
        if (IqcRules.Company(currentUser) is not { } companyId)
            return OperationResult<IqcFileDto>.Fail("Không lấy được công ty hiện tại.");
        var error = IqcRules.Validate(query.Filter);
        if (error is not null) return OperationResult<IqcFileDto>.Fail(error);
        var reader = new IqcReader(context, companyId);
        var data = new QCInputByQCExcelExportData
        {
            DetailRows = await reader.GetExportRowsAsync(query.Filter, ct),
            ReportRows = await reader.GetSummaryRowsForExportAsync(query.Filter, ct)
        };
        return OperationResult<IqcFileDto>.Ok(new IqcFileDto
        {
            Content = renderer.ExportExcel(data),
            FileName = $"QCInputByQC_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        });
    }
}
