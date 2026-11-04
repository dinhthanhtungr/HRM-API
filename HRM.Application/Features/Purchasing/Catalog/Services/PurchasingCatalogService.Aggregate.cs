using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Materials.Services;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed partial class PurchasingCatalogService
{
    internal async Task<OperationResult<CatalogMaterialAggregateSavedDto>> SaveMaterialWithSupplierPricesAsync(
        Guid? id, CatalogMaterialAggregateRequest request, CancellationToken ct)
    {
        var (company, employee) = await RequireWriteContextAsync(ct);
        if (!MaterialSupplierPriceAccess.CanUpdate(user)) throw new UnauthorizedAccessException("Không có quyền sửa giá NCC.");
        OperationResult<CatalogMaterialAggregateSavedDto> Invalid(string message) => OperationResult<CatalogMaterialAggregateSavedDto>.Fail(message);
        if (request.Material is null || request.SupplierPrices is null || request.SupplierPrices.Count > 100)
            return Invalid("Material và supplierPrices hợp lệ là bắt buộc; tối đa 100 NCC.");
        var seen = new HashSet<Guid>();
        foreach (var row in request.SupplierPrices)
        {
            if (row is null) return Invalid("Dòng giá không được null.");
            if ((row.MaterialsSupplierId.HasValue == row.SupplierId.HasValue) ||
                row.MaterialsSupplierId == Guid.Empty || row.SupplierId == Guid.Empty)
                return Invalid("Mỗi dòng phải gửi materialsSupplierId để sửa hoặc supplierId để thêm mới.");
            if (row.Operation is not null && row.Operation != CatalogSupplierPriceWriteRequest.UpdateOperation &&
                row.Operation != CatalogSupplierPriceWriteRequest.DeactivateOperation)
                return Invalid("Operation phải là update hoặc deactivate.");
            if (row.IsDeactivation)
            {
                if (!id.HasValue || !row.MaterialsSupplierId.HasValue)
                    return Invalid("Chỉ được ngừng liên kết hiện hữu khi PATCH NVL.");
                if (row.NewPrice.HasValue || row.Currency is not null || row.IsPreferred.HasValue ||
                    row.MinDeliveryDays.HasValue || row.ClearMinDeliveryDays)
                    return Invalid("Dòng deactivate không được kèm thay đổi giá, ưu tiên hoặc ngày giao.");
                continue; // Scope and strict baselines are validated with all other plans below.
            }
            if (!id.HasValue && row.MaterialsSupplierId.HasValue) return Invalid("NVL mới không nhận liên kết hiện hữu.");
            if (MaterialSupplierManagementRules.ValidatePrice(row.NewPrice, !row.MaterialsSupplierId.HasValue, "NewPrice") is { } priceError) return Invalid(priceError);
            if (MaterialSupplierManagementRules.ValidateCurrency(row.Currency) is { } currencyError) return Invalid(currencyError);
            if (MaterialSupplierManagementRules.ValidateDeliveryDays(row.MinDeliveryDays) is { } daysError) return Invalid(daysError);
            if (row.ClearMinDeliveryDays && row.MinDeliveryDays.HasValue) return Invalid("Không được vừa gửi ngày giao vừa xóa ngày giao.");
            if (row.MaterialsSupplierId.HasValue && !row.NewPrice.HasValue && row.Currency is null &&
                !row.IsPreferred.HasValue && !row.MinDeliveryDays.HasValue && !row.ClearMinDeliveryDays)
                return Invalid("Dòng NCC chưa có thay đổi cần lưu.");
        }
        if (request.SupplierPrices.Count(x => x.IsPreferred == true) > 1) return Invalid("Chỉ được chọn một NCC ưu tiên cho NVL.");

        await using var tx = await db.BeginTransactionAsync(ct);
        await db.LockPurchasingCatalogAsync(company, ct);
        if (id.HasValue)
        {
            await db.LockMaterialSupplierPricesAsync(id.Value, company, ct);
            var current = await db.Materials.AsNoTracking().Where(x => x.MaterialId == id && x.CompanyId == company && x.IsActive == true)
                .Select(x => new { x.UpdatedDate }).FirstOrDefaultAsync(ct);
            if (current is not null && current.UpdatedDate != request.Material.ExpectedUpdatedDate)
                throw new DbUpdateConcurrencyException("Phiên bản NVL đã thay đổi.");
        }
        var staged = await StageMaterialAsync(id, request.Material, company, employee, ct);
        if (!staged.Success) return Invalid(staged.Message ?? "Không lưu được NVL.");
        var materialId = staged.Data!.Id;
        var links = await db.MaterialsSuppliers.Where(x => x.MaterialId == materialId).ToListAsync(ct);
        var supplierIds = request.SupplierPrices.Where(x => x.SupplierId.HasValue).Select(x => x.SupplierId!.Value)
            .Concat(links.Where(x => request.SupplierPrices.Any(r => r.MaterialsSupplierId == x.MaterialsSuppliersId)).Select(x => x.SupplierId)).Distinct().ToArray();
        var allowedSuppliers = await db.Suppliers.AsNoTracking().Where(x => supplierIds.Contains(x.SupplierId) && x.CompanyId == company && x.IsActive == true)
            .Select(x => x.SupplierId).ToListAsync(ct);
        var plans = new List<(CatalogSupplierPriceWriteRequest Row, MaterialsSupplier Link, bool IsNew)>();
        foreach (var row in request.SupplierPrices)
        {
            MaterialsSupplier link;
            var isNew = !row.MaterialsSupplierId.HasValue;
            if (isNew)
            {
                var supplier = row.SupplierId!.Value;
                if (!allowedSuppliers.Contains(supplier)) return Invalid("NCC không tồn tại, đã ngừng hoặc khác công ty.");
                if (links.Any(x => x.SupplierId == supplier)) return Invalid("NCC đã có liên kết; dùng materialsSupplierId hoặc khôi phục liên kết trước.");
                if (row.ExpectedUpdatedDate.HasValue || row.ExpectedCurrentPrice.HasValue || row.ExpectedCurrency is not null)
                    return Invalid("Liên kết mới không nhận baseline cũ.");
                link = new MaterialsSupplier { MaterialsSuppliersId = Guid.CreateVersion7(), MaterialId = materialId,
                    SupplierId = supplier, IsActive = true, IsPreferred = false, CreateDate = staged.Data.UpdatedDate, CreatedBy = employee };
            }
            else
            {
                link = links.FirstOrDefault(x => x.MaterialsSuppliersId == row.MaterialsSupplierId)!;
                if (link is null || link.IsActive != true || !allowedSuppliers.Contains(link.SupplierId))
                    return Invalid("Liên kết không thuộc NVL/công ty hoặc đã ngừng.");
                var expectedPrice = row.ExpectedCurrentPrice.HasValue ? MaterialSupplierManagementRules.RoundPrice(row.ExpectedCurrentPrice.Value) : (decimal?)null;
                if (link.UpdatedDate != row.ExpectedUpdatedDate || link.CurrentPrice != expectedPrice ||
                    !string.Equals(link.Currency, row.ExpectedCurrency, StringComparison.Ordinal))
                    throw new DbUpdateConcurrencyException("Giá hoặc phiên bản liên kết đã thay đổi.");
            }
            if (!seen.Add(link.SupplierId)) return Invalid("Không được gửi trùng NCC trong một lần lưu.");
            plans.Add((row, link, isNew));
        }

        var preferredPlan = plans.Where(x => x.Row.IsPreferred == true).Select(x => x.Link).SingleOrDefault();
        if (preferredPlan is not null)
        {
            var clear = links.Where(x => x.IsPreferred == true && x.MaterialsSuppliersId != preferredPlan.MaterialsSuppliersId).ToList();
            var clearIds = clear.Select(x => x.MaterialsSuppliersId).ToArray();
            if (clearIds.Length > 0 && await db.MaterialsSuppliers.AnyAsync(x => clearIds.Contains(x.MaterialsSuppliersId) && x.Supplier.CompanyId != company, ct))
                return Invalid("Liên kết ưu tiên cũ có dữ liệu khác công ty; cần kiểm tra trước khi sửa.");
            foreach (var other in clear)
            {
                other.IsPreferred = false;
                other.UpdatedDate = NextUpdate(other.UpdatedDate);
                other.UpdatedBy = employee;
            }
            // Flush old preferred flags first, inside the SAME transaction, to respect the unique index.
            if (clear.Count > 0) await db.SaveChangesAsync(ct);
        }
        else if (links.Count(x => x.IsPreferred == true && !plans.Any(p => p.Link == x && (p.Row.IsPreferred == false || p.Row.IsDeactivation))) > 1)
            return Invalid("Dữ liệu có nhiều NCC ưu tiên; cần chọn rõ một NCC để sửa.");

        var result = new List<CatalogSupplierPriceSavedDto>();
        foreach (var plan in plans)
        {
            var link = plan.Link;
            Guid? historyId = null;
            var oldPrice = link.CurrentPrice;
            var oldCurrency = link.Currency;
            var now = NextUpdate(link.UpdatedDate);
            if (plan.IsNew || plan.Row.NewPrice.HasValue || plan.Row.Currency is not null)
            {
                historyId = Guid.CreateVersion7();
                db.PriceHistories.Add(new PriceHistory { PriceHistoryId = historyId.Value, MaterialsSuppliersId = link.MaterialsSuppliersId,
                    OldPrice = oldPrice, Currency = oldCurrency, CreateDate = now, CreatedBy = employee });
            }
            if (plan.Row.IsDeactivation)
            {
                link.IsActive = false;
                link.IsPreferred = false;
            }
            if (plan.Row.NewPrice.HasValue) link.CurrentPrice = MaterialSupplierManagementRules.RoundPrice(plan.Row.NewPrice.Value);
            link.Currency = plan.Row.Currency is null ? plan.IsNew ? "VND" : oldCurrency : MaterialSupplierManagementRules.NormalizeCurrency(plan.Row.Currency);
            if (plan.Row.IsPreferred.HasValue) link.IsPreferred = plan.Row.IsPreferred.Value;
            if (plan.Row.ClearMinDeliveryDays) link.MinDeliveryDays = null;
            else if (plan.Row.MinDeliveryDays.HasValue) link.MinDeliveryDays = plan.Row.MinDeliveryDays.Value;
            link.UpdatedDate = now; link.UpdatedBy = employee;
            if (plan.IsNew) db.MaterialsSuppliers.Add(link);
            result.Add(new(link.MaterialsSuppliersId, link.SupplierId, link.CurrentPrice, link.Currency, now, historyId,
                oldPrice != link.CurrentPrice || !string.Equals(oldCurrency, link.Currency, StringComparison.OrdinalIgnoreCase)));
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OperationResult<CatalogMaterialAggregateSavedDto>.Ok(new CatalogMaterialAggregateSavedDto(staged.Data, result));
    }
}
