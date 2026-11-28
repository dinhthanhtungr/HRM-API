using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Queries.GetLabTestLookup;
using HRM.Application.Features.DevAndQA.LabTests.Services.Basic;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Identity;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class BasicLabLookupTests
{
    [Fact]
    public async Task SelectedCreatorMustBeActiveLabInCompanyAndPatchPreservesCreationDate()
    {
        var (db, store) = IqcTestStore.CreateContext<ILabTestDbContext>();
        var user = new IqcWorkflowTests.User();
        store.ReflectAddedOnSave = true;
        var actor = new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId, IsActive = true };
        var first = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId, FullName = "Lab A", IsActive = true, Status = "Active" };
        var second = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId, FullName = "Lab B", IsActive = true, Status = "Active" };
        var foreign = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true, Status = "Active" };
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "LabUser", NormalizedName = "LABUSER" };
        var accounts = new[] { first, second, foreign }.Select(x => new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = x.EmployeeId }).ToArray();
        store.Seed("Employees", actor, first, second, foreign);
        store.Seed("Users", accounts);
        store.Seed("Roles", role);
        store.Seed("UserRoles", accounts.Select(x => new ApplicationUserRole { UserId = x.Id, RoleId = role.Id, IsActive = true }).ToArray());
        var service = Service(db, user);
        var saved = await service.SaveAsync(null, new("Sample", new(2026, 11, 15), Results: [], CreatedBy: first.EmployeeId), default);
        Assert.Equal(first.EmployeeId, saved.CreatedBy);
        Assert.Equal("Lab A", saved.CreatedByName);
        var updated = await service.SaveAsync(saved.Id, new(CreatedBy: second.EmployeeId), default);
        Assert.Equal(second.EmployeeId, updated.CreatedBy);
        Assert.Equal(saved.CreatedDate, updated.CreatedDate);
        Assert.Equal(user.EmployeeId, store.Added.OfType<HRM.Domain.Entities.DevandqaSchema.LabTest>().Single().UpdatedBy);
        second.IsActive = false;
        Assert.Equal(second.EmployeeId, (await service.SaveAsync(saved.Id, new(Note: "unchanged creator"), default)).CreatedBy);
        foreach (var invalid in new[] { actor.EmployeeId, second.EmployeeId, foreign.EmployeeId, Guid.Empty })
            await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(saved.Id, new(CreatedBy: invalid), default));
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(saved.Id, new(ClearFields: ["createdBy"]), default));
    }
    [Fact]
    public async Task LabLookupRequiresActiveLabRoleEmployeeAndCompany()
    {
        var (db, store) = IqcTestStore.CreateContext<ILabTestDbContext>();
        var user = new IqcWorkflowTests.User();
        var service = Service(db, user);
        var lab = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId, FullName = "Lab", ExternalId = "LAB", IsActive = true, Status = "Active" };
        var foreign = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), FullName = "Foreign", IsActive = true, Status = "Active" };
        var nonLab = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId, FullName = "QC", IsActive = true, Status = "Active" };
        var account = new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = lab.EmployeeId };
        var foreignAccount = new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = foreign.EmployeeId };
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "LabUser", NormalizedName = "LABUSER" };
        var assignment = new ApplicationUserRole { UserId = account.Id, RoleId = role.Id, IsActive = true };
        store.Seed("Employees", lab, foreign, nonLab);
        store.Seed("Users", account, foreignAccount);
        store.Seed("Roles", role);
        store.Seed("UserRoles", assignment, new ApplicationUserRole { UserId = foreignAccount.Id, RoleId = role.Id, IsActive = true });
        var query = new GetLabTestLookupQuery(LabTestLookupKind.LabEmployee);
        Assert.Equal(lab.EmployeeId, Assert.Single((await service.LookupAsync(query, default)).Items).Id);
        assignment.IsActive = false;
        Assert.Empty((await service.LookupAsync(query, default)).Items);
        assignment.IsActive = true; lab.IsActive = false;
        Assert.Empty((await service.LookupAsync(query, default)).Items);
    }

    [Fact]
    public async Task SupplierIsOptionalAndRejectsForeignOrInactiveAssignments()
    {
        var (db, store) = IqcTestStore.CreateContext<ILabTestDbContext>();
        var user = new IqcWorkflowTests.User();
        store.ReflectAddedOnSave = true;
        store.Seed("Employees", new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        var supplier = new Supplier { SupplierId = Guid.NewGuid(), CompanyId = user.CompanyId, SupplierName = "Supplier", IsActive = true };
        var foreign = new Supplier { SupplierId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        store.Seed("Suppliers", supplier, foreign);
        var service = Service(db, user);
        var saved = await service.SaveAsync(null, new("Sample", new(2026, 11, 15), Results: [], SupplierId: supplier.SupplierId, SupplierName: "Forged name"), default);
        Assert.Equal("Supplier", saved.SupplierName);
        Assert.Equal(supplier.SupplierId, saved.SupplierId);
        supplier.SupplierName = "Renamed catalog";
        Assert.Equal("Supplier", (await service.DetailAsync(saved.Id, default)).SupplierName);
        Assert.Equal("Supplier", (await service.SaveAsync(saved.Id, new(Note: "Unrelated change"), default)).SupplierName);
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(saved.Id, new(SupplierName: "Typed without clearing ID"), default));
        var free = await service.SaveAsync(saved.Id, new(SupplierName: "  Free supplier  ", ClearFields: ["supplierId"]), default);
        Assert.Null(free.SupplierId);
        Assert.Equal("Free supplier", free.SupplierName);
        Assert.Equal("Free supplier", (await service.DetailAsync(saved.Id, default)).SupplierName);
        Assert.Null((await service.SaveAsync(saved.Id, new(ClearFields: ["supplierName"]), default)).SupplierName);
        await service.SaveAsync(saved.Id, new(SupplierId: supplier.SupplierId), default);
        Assert.Equal(supplier.SupplierId, (await service.SaveAsync(saved.Id, new(Note: "Note"), default)).SupplierId);
        Assert.Null((await service.SaveAsync(saved.Id, new(ClearFields: ["supplierId"]), default)).SupplierId);
        Assert.Null((await service.DetailAsync(saved.Id, default)).SupplierName);
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(saved.Id, new(SupplierName: new string('x', 301)), default));
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(saved.Id, new(SupplierName: "Free", ClearFields: ["supplierName"]), default));
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(saved.Id, new(SupplierId: foreign.SupplierId), default));
        supplier.IsActive = false;
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(saved.Id, new(SupplierId: supplier.SupplierId), default));
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(saved.Id, new(SupplierId: supplier.SupplierId, ClearFields: ["supplierId"]), default));
    }

    [Theory]
    [InlineData(LabTestLookupKind.Supplier)]
    [InlineData(LabTestLookupKind.LabEmployee)]
    [InlineData(LabTestLookupKind.Material)]
    public void LookupsTranslateToSqlWithoutSensitiveAccountColumns(LabTestLookupKind kind)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new IqcWorkflowTests.User();
        var sql = Service(db, user).BuildLookupQuery(new(kind, "Lab"), user.CompanyId!.Value)
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(20).Take(20).ToQueryString();
        Assert.Contains("LIMIT", sql);
        Assert.Contains("ILIKE", sql);
        Assert.DoesNotContain("passwordhash", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshtoken", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static BasicLabTestService Service(ILabTestDbContext db, IqcWorkflowTests.User user) =>
        new(db, new(db, user, new CurrentUserPermissionService(user)), new());
}
