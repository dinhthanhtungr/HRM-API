using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed partial class PurchasingCatalogService
{
    public Task<OperationResult<CatalogSavedDto>> SetContactActiveAsync(Guid supplierId, Guid id, bool active, DateTime? expected, CancellationToken ct)
        => SetChildActiveAsync(supplierId, id, active, expected, false, ct);
    public Task<OperationResult<CatalogSavedDto>> SetAddressActiveAsync(Guid supplierId, Guid id, bool active, DateTime? expected, CancellationToken ct)
        => SetChildActiveAsync(supplierId, id, active, expected, true, ct);

    private async Task<OperationResult<CatalogSavedDto>> SetChildActiveAsync(Guid supplierId, Guid id, bool active, DateTime? expected, bool address, CancellationToken ct)
    {
        var (companyId, employeeId) = await RequireWriteContextAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.LockPurchasingCatalogAsync(companyId, ct);
        var parent = await db.Suppliers.Include(x => x.SupplierContacts).Include(x => x.SupplierAddresses)
            .FirstOrDefaultAsync(x => x.SupplierId == supplierId && x.CompanyId == companyId && x.IsActive == true, ct);
        if (parent is null) return Fail("Không tìm thấy NCC active.");
        if (CatalogPatch.Conflict(expected, parent.UpdatedDate)) return Fail(ConflictMessage);
        if (address)
        {
            var child = parent.SupplierAddresses.FirstOrDefault(x => x.AddressId == id);
            if (child is null) return Fail("Địa chỉ không thuộc NCC này.");
            if (child.IsActive != active) { child.IsActive = active; child.IsPrimary = false; }
        }
        else
        {
            var child = parent.SupplierContacts.FirstOrDefault(x => x.ContactId == id);
            if (child is null) return Fail("Liên hệ không thuộc NCC này.");
            if (child.IsActive != active) { child.IsActive = active; child.IsPrimary = false; }
        }
        var now = NextUpdate(parent.UpdatedDate);
        parent.UpdatedDate = now; parent.UpdatedBy = employeeId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Saved(id, null, now);
    }
}
