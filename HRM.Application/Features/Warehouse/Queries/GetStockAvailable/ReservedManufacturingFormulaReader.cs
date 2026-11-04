using HRM.Application.Abstractions.Persistence.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetStockAvailable;

/// <summary>Resolve mã công thức đang chọn của các MFG giữ chỗ theo batch, không đọc giá hoặc thành phần.</summary>
internal static class ReservedManufacturingFormulaReader
{
    public static async Task<IReadOnlyDictionary<string, string>> ReadAsync(
        IWarehouseReadDbContext db, Guid companyId, string[] mfgCodes, CancellationToken ct)
    {
        if (mfgCodes.Length == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var selections = await db.ProductionSelectVersions.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ValidTo == null
                && x.MfgProductionOrder.CompanyId == companyId && x.MfgProductionOrder.IsActive
                && mfgCodes.Contains(x.MfgProductionOrder.ExternalId)
                && x.ManufacturingFormula != null && x.ManufacturingFormula.IsActive
                && x.ManufacturingFormula.CompanyId == companyId
                && x.ManufacturingFormula.ExternalId != "")
            .Select(x => new
            {
                MfgCode = x.MfgProductionOrder.ExternalId,
                FormulaCode = x.ManufacturingFormula!.ExternalId,
                x.ValidFrom,
                x.ProductionSelectVersionId
            })
            .ToListAsync(ct);

        // Match the MFG list's current-selection ordering, including legacy null ValidFrom.
        return selections.GroupBy(x => x.MfgCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                g => g.OrderByDescending(x => x.ValidFrom)
                    .ThenByDescending(x => x.ProductionSelectVersionId).First().FormulaCode,
                StringComparer.Ordinal);
    }
}
