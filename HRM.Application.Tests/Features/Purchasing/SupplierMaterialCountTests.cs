using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.Catalog.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.MaterialSchema;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class SupplierMaterialCountTests
{
    [Fact]
    public async Task Count_IsDistinctActiveMaterialsInCompany_IndependentOfInactiveParentFlag()
    {
        var (db, store) = IqcTestStore.CreateContext<IPLMWriteDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var company = user.CompanyId!.Value;
        var active = new Material { MaterialId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var inactive = new Material { MaterialId = Guid.NewGuid(), CompanyId = company, IsActive = false };
        var foreign = new Material { MaterialId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        var supplier = new Supplier
        {
            SupplierId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            MaterialsSuppliers =
            [
                new() { MaterialId = active.MaterialId, Material = active, IsActive = true },
                new() { MaterialId = active.MaterialId, Material = active, IsActive = true },
                new() { MaterialId = inactive.MaterialId, Material = inactive, IsActive = true },
                new() { MaterialId = foreign.MaterialId, Material = foreign, IsActive = true },
                new() { MaterialId = Guid.NewGuid(), Material = active, IsActive = false }
            ]
        };
        store.Seed(nameof(db.Suppliers), supplier);
        var service = new PurchasingCatalogService(db, user, new CurrentUserPermissionService(user), null!, null!);
        Assert.Equal(1, (await service.GetSupplierAsync(supplier.SupplierId, false, default))!.MaterialCount);
        supplier.IsActive = false;
        Assert.Null(await service.GetSupplierAsync(supplier.SupplierId, false, default));
        Assert.Equal(1, (await service.GetSupplierAsync(supplier.SupplierId, true, default))!.MaterialCount);
        active.IsActive = false;
        Assert.Equal(0, (await service.GetSupplierAsync(supplier.SupplierId, true, default))!.MaterialCount);
        supplier.CompanyId = Guid.NewGuid();
        Assert.Null(await service.GetSupplierAsync(supplier.SupplierId, true, default));
    }
}
