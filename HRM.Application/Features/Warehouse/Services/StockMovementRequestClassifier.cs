using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Services;

internal static class StockMovementRequestClassifier
{
    // Legacy CT requests use ExportOther. Type 10 alone does not establish a mixing destination.
    public static IQueryable<string> MixingExportCodes(IWarehouseReadDbContext db, Guid company)
        => db.WarehouseRequests.AsNoTracking().Where(x => x.CompanyId == company
            && x.ReqType == WareHouseRequestType.ExportOther
            && x.RequestName.Trim().ToUpper() == "PHIẾU XUẤT KHO CHO CT")
            .Select(x => x.RequestCode);
}
