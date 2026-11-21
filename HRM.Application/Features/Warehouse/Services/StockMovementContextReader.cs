using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Services;

internal sealed record StockMovementContext(
    IReadOnlyDictionary<Guid, string> Employees,
    IReadOnlyDictionary<long, string> Vouchers)
{
    public IReadOnlySet<string> MixingExportRequests { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}

/// <summary>Batch-loads only the company-scoped display names and document codes needed for a page.</summary>
internal static class StockMovementContextReader
{
    public static async Task<StockMovementContext> ReadAsync(IWarehouseReadDbContext db, Guid company,
        IReadOnlyList<WarehouseMovementRow> rows, CancellationToken ct, WarehouseMovementQueryTiming? timing = null)
    {
        var employeeIds = rows.Select(x => x.CreatedBy).OfType<Guid>().Distinct().ToArray();
        var voucherIds = rows.Select(x => x.VoucherId).OfType<long>().Distinct().ToArray();
        var employees = await TimeAsync(timing, "actors", () => db.Employees.AsNoTracking()
            .Where(x => x.CompanyId == company && employeeIds.Contains(x.EmployeeId))
            .Select(x => new { x.EmployeeId, x.FullName })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.FullName, ct));
        var vouchers = await TimeAsync(timing, "vouchers", () => db.WarehouseVouchers.AsNoTracking()
            .Where(x => x.CompanyId == company && voucherIds.Contains(x.VoucherId))
            .Select(x => new { x.VoucherId, x.VoucherCode })
            .ToDictionaryAsync(x => x.VoucherId, x => x.VoucherCode, ct));
        var requestCodes = rows.Where(x => x.CompanyId == company && x.MovementType == "NORMAL_OUT"
                && x.Scope == "NORMAL" && x.DeltaKg < 0)
            .Select(x => x.RequestCode).OfType<string>().Distinct().ToArray();
        var mixingRequests = requestCodes.Length == 0 ? [] : await TimeAsync(timing, "requests", () => StockMovementRequestClassifier.MixingExportCodes(db, company)
            .Where(x => requestCodes.Contains(x)).ToArrayAsync(ct));
        return new(employees, vouchers) { MixingExportRequests = mixingRequests.ToHashSet(StringComparer.Ordinal) };
    }

    private static Task<T> TimeAsync<T>(WarehouseMovementQueryTiming? timing, string stage, Func<Task<T>> action)
        => timing?.RunAsync(stage, action) ?? action();
}
