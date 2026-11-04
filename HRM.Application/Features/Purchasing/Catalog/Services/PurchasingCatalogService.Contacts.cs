using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed partial class PurchasingCatalogService
{
    public async Task<OperationResult<CatalogSavedDto>> SaveContactAsync(Guid supplierId, Guid? id, CatalogContactWriteRequest request, CancellationToken ct)
    {
        var (companyId, employeeId) = await RequireWriteContextAsync(ct);
        var error = CatalogPatch.Validate(request, "firstName", "lastName", "gender", "phone", "email");
        if (error is not null) return Fail(error);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.LockPurchasingCatalogAsync(companyId, ct);
        var parent = await db.Suppliers.Include(x => x.SupplierContacts)
            .FirstOrDefaultAsync(x => x.SupplierId == supplierId && x.CompanyId == companyId && x.IsActive == true, ct);
        if (parent is null) return Fail("Không tìm thấy NCC active.");
        if (CatalogPatch.Conflict(request.ExpectedUpdatedDate, parent.UpdatedDate)) return Fail(ConflictMessage);
        var entity = id.HasValue ? parent.SupplierContacts.FirstOrDefault(x => x.ContactId == id) : null;
        if (id.HasValue && (entity is null || entity.IsActive != true)) return Fail("Không tìm thấy bản ghi active thuộc NCC này.");
        if (entity is null)
        {
            entity = new SupplierContact { ContactId = Guid.CreateVersion7(), SupplierId = supplierId, IsActive = true, IsPrimary = false };
        }
        CatalogPatch.Text(request.FirstName, request, nameof(request.FirstName), () => entity.FirstName, value => entity.FirstName = value);
        CatalogPatch.Text(request.LastName, request, nameof(request.LastName), () => entity.LastName, value => entity.LastName = value);
        CatalogPatch.Text(request.Gender, request, nameof(request.Gender), () => entity.Gender, value => entity.Gender = value);
        CatalogPatch.Text(request.Phone, request, nameof(request.Phone), () => entity.Phone, value => entity.Phone = value);
        CatalogPatch.Text(request.Email, request, nameof(request.Email), () => entity.Email, value => entity.Email = value);
        if (string.IsNullOrWhiteSpace(entity.FirstName) && string.IsNullOrWhiteSpace(entity.LastName) && string.IsNullOrWhiteSpace(entity.Phone) && string.IsNullOrWhiteSpace(entity.Email))
            return Fail("Liên hệ cần tên, điện thoại hoặc email.");
        if (request.IsPrimary.HasValue) entity.IsPrimary = request.IsPrimary.Value;
        if (request.IsPrimary == true)
            foreach (var other in parent.SupplierContacts.Where(x => x.ContactId != entity.ContactId)) other.IsPrimary = false;
        else if (parent.SupplierContacts.Count(x => x.ContactId != entity.ContactId && x.IsActive == true && x.IsPrimary == true) + (entity.IsPrimary == true ? 1 : 0) > 1)
            return Fail("Dữ liệu có nhiều bản ghi mặc định. Hãy chọn rõ một bản ghi mặc định.");
        if (!id.HasValue)
        {
            parent.SupplierContacts.Add(entity);
            // GUID đã được cấp ở Application; đánh dấu INSERT rõ ràng dù model có default UUID ở DB.
            await db.SupplierContacts.AddAsync(entity, ct);
        }
        var now = NextUpdate(parent.UpdatedDate);
        parent.UpdatedDate = now; parent.UpdatedBy = employeeId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Saved(entity.ContactId, null, now);
    }
}
