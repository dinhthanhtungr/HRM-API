using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

internal static class ManufacturingFormulaHistoryReader
{
    public static async Task FillAsync(
        IManufacturingDbContext db,
        Guid company,
        List<GetSummaryMfgProductionOrder> items,
        CancellationToken ct
    )
    {
        if (items.Count == 0)
        {
            return;
        }

        var ids = items.Select(x => x.MfgProductionOrderId).ToArray();
        var histories = await (
            from v in db.ProductionSelectVersions.AsNoTracking()join f in db.ManufacturingFormulas.AsNoTracking()on v.ManufacturingFormulaId equals f.ManufacturingFormulaId
            where v.CompanyId == company && f.CompanyId == company && ids.Contains(v.MfgProductionOrderId)select new
            {
                v.MfgProductionOrderId,
                f.ExternalId,
                CreatedDate = v.ValidFrom ?? f.CreatedDate
            }

        ).ToListAsync(ct);
        var codes = histories.Select(x => x.ExternalId).Distinct().ToArray();
        // Legacy totals group by VA code, not by MFG. Operators provide the company boundary
        // because these existing shift-report tables have no CompanyId column.
        var quantities = await (
            from h in db.EndOfShiftReportForAlls.AsNoTracking()join d in db.EndOfShiftReportDetailForAlls.AsNoTracking()on h.ShiftReportForAllId equals d.ShiftReportForAllId
            join employee in db.Employees.AsNoTracking()on h.Operator equals employee.EmployeeId
            where employee.CompanyId == company && h.ExternalId != null && d.ExternalId != null
            && codes.Contains(h.ExternalId)
            && codes.Contains(d.ExternalId)group new
            {
                h,
                d
            }

            by h.ExternalId into g
                select new
                {
                    Code = g.Key!,
                    Total = g.Sum(x => x.d.WeightStockedKg ?? x.d.NetWeight ?? 0),
                    Good = g.Sum(x => x.h.ProductStatus == "Reported" || x.d.ProductType == "Thanh pham" ? x.d.WeightStockedKg ?? x.d.NetWeight ?? 0 : 0),
                    Error = g.Sum(x => x.h.ProductStatus == "Error" || x.h.ProductStatus == "NG" ? x.d.WeightStockedKg ?? x.d.NetWeight ?? 0 : 0)
                }

        ).ToDictionaryAsync(x => x.Code, ct);
        var byOrder = histories.ToLookup(x => x.MfgProductionOrderId);
        foreach (var item in items)
        {
            item.MFGFormulaHistories = byOrder[item.MfgProductionOrderId].OrderByDescending(x => x.CreatedDate).Select(h =>
            {
                quantities.TryGetValue(h.ExternalId, out var quantity);
                return new GetMFGFormulaHistories

                {

                    MfgFormulaExternalId = h.ExternalId,

                    CreatedDate = h.CreatedDate,

                    TotalQuantity = quantity?.Total ?? 0,

                    GoodQuantity = quantity?.Good ?? 0,

                    ErrorQuantity = quantity?.Error ?? 0

                };
            })
                .ToList();
        }
    }
}
