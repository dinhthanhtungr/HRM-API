using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcEvaluationHistory;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.DevAndQA.IQC.Queries.ExportIqcEvaluationExcel;
/// <summary>Đọc snapshot được scope, ghép metadata thật và chuyển renderer; không lưu DB.</summary>
internal sealed class ExportIqcEvaluationExcelQueryHandler(IEvaluationDbContext db,
    EvaluationAccessService access, IqcEvaluationService evaluations,
    IqcEvaluationHistoryReader history, IIqcEvaluationExcelRenderer renderer)
    : IRequestHandler<ExportIqcEvaluationExcelQuery, IqcFileDto>
{
    public async Task<IqcFileDto> Handle(ExportIqcEvaluationExcelQuery request, CancellationToken ct)
    {
        var current = await evaluations.GetIqcEvaluationAsync(request.VoucherDetailId, ct);
        if (!current.IsStored || current.Results.Count == 0)
            throw new EvaluationRequestException("Phiếu chưa có kết quả đánh giá đã lưu để xuất.", 409);
        var previous = await history.GetIqcEvaluationHistoryAsync(
            new(request.VoucherDetailId, request.PageNumber, request.PageSize), ct);
        var company = access.Company(false, HRM.Domain.Enums.Devandqa.EvaluationTemplateType.Iqc);
        var ids = previous.Items.Select(x => x.VoucherDetailId).Append(request.VoucherDetailId).Distinct().ToArray();
        var details = await db.WarehouseVoucherDetails.AsNoTracking()
            .Where(x => ids.Contains(x.VoucherDetailId) && x.Voucher.CompanyId == company)
            .Select(x => new { x.VoucherDetailId, x.ProductCode, x.ProductName, x.LotNumber }).ToListAsync(ct);
        var byId = details.ToDictionary(x => x.VoucherDetailId);
        var detail = byId[request.VoucherDetailId];
        var date = await db.QCInputByQCs.AsNoTracking()
            .Where(x => x.QCInputByQCId == current.QCInputByQCId && x.WarehouseVoucherDetail.Voucher.CompanyId == company)
            .Select(x => x.CreatedDate).SingleAsync(ct);
        var columns = new List<IqcEvaluationExportColumn>
        {
            new(date, detail.ProductCode, detail.ProductName, detail.LotNumber, current)
        };
        foreach (var item in previous.Items)
        {
            var old = byId[item.VoucherDetailId];
            columns.Add(new(item.QCCreatedDate, old.ProductCode, old.ProductName, item.LotNumber, item.Evaluation));
        }
        ct.ThrowIfCancellationRequested();
        return new IqcFileDto { Content = renderer.Render(columns),
            FileName = $"IQC_Results_{request.VoucherDetailId}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx" };
    }
}
