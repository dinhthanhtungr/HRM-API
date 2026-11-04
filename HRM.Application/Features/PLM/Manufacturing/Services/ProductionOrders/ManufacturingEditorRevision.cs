using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Domain.Entities.ManufacturingSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;

internal static class ManufacturingEditorRevision
{
    // Không thêm cột version. Token bao gồm header và các lựa chọn/VA đang mở;
    // kiểm tra lại sau khóa MFG trong writer, không dùng token làm quyền truy cập.
    public static async Task<string> ReadAsync(IManufacturingDbContext db, MfgProductionOrder order, CancellationToken ct)
    {
        var selections = await db.ProductionSelectVersions.AsNoTracking()
            .Where(x => x.CompanyId == order.CompanyId && x.MfgProductionOrderId == order.MfgProductionOrderId && x.ValidTo == null)
            .OrderBy(x => x.ProductionSelectVersionId)
            .Select(x => new { x.ProductionSelectVersionId, x.ManufacturingFormulaId, x.ValidFrom,
                UpdatedDate = x.ManufacturingFormula != null && x.ManufacturingFormula.CompanyId == order.CompanyId
                    ? (DateTime?)x.ManufacturingFormula.UpdatedDate : null })
            .ToListAsync(ct);
        var content = JsonSerializer.Serialize(new
        {
            order.MfgProductionOrderId, order.Status, order.UpdatedDate, order.TotalQuantity, order.NumOfBatches,
            order.StepOfProduct, order.ExpectedDate, order.ManufacturingDate, order.BagType,
            order.LabNote, order.PlpuNote, order.QcCheck, order.Requirement, selections
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    public static async Task<bool> MatchesAsync(IManufacturingDbContext db, MfgProductionOrder order, string? expected, CancellationToken ct)
        => expected is null || string.Equals(expected, await ReadAsync(db, order, ct), StringComparison.Ordinal);

    public const string Conflict = "Lệnh hoặc công thức đã thay đổi. Vui lòng tải lại trước khi lưu; không gửi lại thao tác tự động.";
}
