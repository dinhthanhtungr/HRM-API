using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Application.Features.Warehouse.Services;

public interface IWarehouseStockVisibilityService
{
    Task<IQueryable<WarehouseShelfStock>> ApplyAsync(
        IQueryable<WarehouseShelfStock> stockQuery,
        Guid companyId,
        string? keyword,
        CancellationToken cancellationToken = default);

    Task<IQueryable<WarehouseVoucherDetail>> ApplyVoucherDetailsAsync(
        IQueryable<WarehouseVoucherDetail> detailQuery,
        Guid companyId,
        string? keyword,
        CancellationToken cancellationToken = default);
}
