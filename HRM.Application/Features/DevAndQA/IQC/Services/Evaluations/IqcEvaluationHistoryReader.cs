using HRM.Application.Commons.Pagination;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcEvaluationHistory;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;

internal sealed class IqcEvaluationHistoryReader(IEvaluationDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
{
    /// <summary>Đọc lịch sử từ các QC khác cùng mặt hàng, giữ snapshot; không ghi DB hoặc dựng từ template hiện tại.</summary>
    public async Task<PagedResult<IqcEvaluationHistoryDto>> GetIqcEvaluationHistoryAsync(
        GetIqcEvaluationHistoryQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty)
            throw new EvaluationRequestException("Không lấy được công ty hiện tại.", 403);
        if (!permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewIqc))
            throw new EvaluationRequestException("Không có quyền xem IQC.", 403);
        if (request.VoucherDetailId <= 0) throw new EvaluationRequestException("VoucherDetailId không hợp lệ.");
        if (request.PageSize < 1 || request.PageSize > 50) throw new EvaluationRequestException("PageSize phải từ 1 đến 50.");
        var offset = ((long)request.PageNumber - 1) * request.PageSize;
        if (request.PageNumber < 1 || offset > int.MaxValue)
            throw new EvaluationRequestException("PageNumber không hợp lệ.");
        var skip = (int)offset;
        var detail = await db.WarehouseVoucherDetails.AsNoTracking()
            .Where(x => x.VoucherDetailId == request.VoucherDetailId && x.Voucher.CompanyId == company)
            .Select(x => new { x.ProductCode }).FirstOrDefaultAsync(ct)
            ?? throw new EvaluationRequestException("Không tìm thấy dòng phiếu trong công ty hiện tại.", 404);
        if (string.IsNullOrWhiteSpace(detail.ProductCode))
            return new([], 0, request.PageNumber, request.PageSize);

        var currentDate = await db.QCInputByQCs.AsNoTracking()
            .Where(x => x.VoucherDetailId == request.VoucherDetailId && x.WarehouseVoucherDetail.Voucher.CompanyId == company)
            .OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.QCInputByQCId)
            .Select(x => (DateTime?)x.CreatedDate).FirstOrDefaultAsync(ct);
        var query = BuildIqcHistoryQuery(company, request.VoucherDetailId, detail.ProductCode, currentDate);
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(x => x.QCInputByQC.CreatedDate)
            .ThenByDescending(x => x.QCInputByQCId).ThenByDescending(x => x.Id)
            .Skip(skip).Take(request.PageSize)
            .Select(x => new
            {
                x.Id, x.QCInputByQCId, x.QCInputByQC.VoucherDetailId,
                x.QCInputByQC.CreatedDate, x.QCInputByQC.WarehouseVoucherDetail.LotNumber,
                x.QCInputByQC.WarehouseVoucherDetail.ProductCode,
                x.QCInputByQC.WarehouseVoucherDetail.QtyKg
            }).ToListAsync(ct);
        var ids = page.Select(x => x.Id).ToArray();
        var evaluations = await db.IqcInspectionEvaluations.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.QCInputByQC.WarehouseVoucherDetail.Voucher.CompanyId == company)
            .Include(x => x.Results).ThenInclude(x => x.IqcSpecificationItem).ThenInclude(x => x!.IqcSpecification)
            .ToListAsync(ct);
        var byId = evaluations.ToDictionary(x => x.Id);
        var items = new List<IqcEvaluationHistoryDto>();
        foreach (var row in page)
        {
            if (!byId.TryGetValue(row.Id, out var evaluation)) continue;
            IqcEvaluationMapper.ValidateLegacy(evaluation, company);
            items.Add(new(row.QCInputByQCId, row.VoucherDetailId, row.CreatedDate, row.LotNumber,
                row.ProductCode, IqcEvaluationMapper.ToDto(evaluation), row.QtyKg, "kg"));
        }
        return new(items, total, request.PageNumber, request.PageSize);
    }

    // Giữ query riêng để kiểm tra translation PostgreSQL và toàn bộ điều kiện scope trước pagination.
    internal IQueryable<IqcInspectionEvaluation> BuildIqcHistoryQuery(Guid company, long currentDetailId,
        string materialCode, DateTime? currentDate) => db.IqcInspectionEvaluations.AsNoTracking()
        .Where(x => x.QCInputByQC.WarehouseVoucherDetail.Voucher.CompanyId == company &&
            x.QCInputByQC.WarehouseVoucherDetail.ProductCode == materialCode &&
            x.QCInputByQC.VoucherDetailId != currentDetailId &&
            (!currentDate.HasValue || x.QCInputByQC.CreatedDate <= currentDate.Value) && x.Results.Any());
}
