using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.MRO.Equipment.Dtos;
using HRM.Application.Features.MRO.Equipment.Services;
using HRM.Domain.Entities.MROSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.MRO;

public sealed class EquipmentManagementTests
{
    [Theory]
    [InlineData(ApplicationRoles.Admin, true, true, true)]
    [InlineData(ApplicationRoles.Developer, true, true, true)]
    [InlineData(ApplicationRoles.President, true, true, true)]
    [InlineData(ApplicationRoles.Maintenance.MaintenanceUser, true, true, false)]
    [InlineData(ApplicationRoles.Production.ManufactureUser, true, false, false)]
    [InlineData(ApplicationRoles.Production.QLSXUser, true, false, false)]
    [InlineData(ApplicationRoles.Sales.SaleUser, false, false, false)]
    [InlineData(ApplicationRoles.Lab.LabUser, false, false, false)]
    public void Capabilities_RespectApprovedRoleMatrix(string role, bool view, bool edit, bool delete)
    {
        var user = new TestUser { Roles = [role] };
        Assert.Equal(new EquipmentCapabilities(view, edit, edit, delete), Service(user).GetCapabilities());
    }

    [Fact]
    public async Task ExplicitEmptyPermissions_AndMissingContextFailBeforeDatabaseAccess()
    {
        foreach (var user in new[] {
            new TestUser { Explicit = true }, new TestUser { IsAuthenticated = false },
            new TestUser { CompanyId = null }, new TestUser { CompanyId = Guid.Empty }
        })
        {
            var service = Service(user);
            Assert.Equal(new EquipmentCapabilities(false, false, false, false), service.GetCapabilities());
            Assert.Equal(403, (await service.GetListAsync(new(), default)).StatusCode);
            Assert.Equal(403, (await service.GetDetailAsync(1, default)).StatusCode);
            Assert.Equal(403, (await service.GetOptionsAsync(default)).StatusCode);
            Assert.Equal(403, (await service.SaveAsync(null, new(), default)).StatusCode);
            Assert.Equal(403, (await service.SaveAsync(1, new(), default)).StatusCode);
            Assert.Equal(403, (await service.DeleteAsync(1, default)).StatusCode);
            Assert.Equal(403, (await service.GetSpecsAsync(1, default)).StatusCode);
            Assert.Equal(403, (await service.SaveSpecAsync(1, null, new(), default)).StatusCode);
            Assert.Equal(403, (await service.SaveSpecAsync(1, 1, new(), default)).StatusCode);
            Assert.Equal(403, (await service.DeleteSpecAsync(1, 1, default)).StatusCode);
        }
        Assert.False(new CurrentUserPermissionService(new TestUser()).HasPermission("mro.unknown"));
    }

    [Fact]
    public void ExplicitPermissions_DoNotFallBackToAdminRole()
    {
        var user = new TestUser { Explicit = true, Permissions = [ApplicationPermissions.Equipment.View] };
        Assert.Equal(new EquipmentCapabilities(true, false, false, false), Service(user).GetCapabilities());
    }

    [Fact]
    public void Scope_RejectsOtherCompany_AndMismatchedSpecificationParent()
    {
        var company = Guid.NewGuid();
        var own = new EquipmentMRO { EquipmentId = 1, FactoryId = company };
        var foreign = new EquipmentMRO { EquipmentId = 2, FactoryId = Guid.NewGuid() };
        var machines = new[] { own, foreign }.AsQueryable();
        Assert.Same(own, Assert.Single(EquipmentScope.ForCompany(machines, company)));
        Assert.Empty(EquipmentScope.ForCompany(machines, null));
        Assert.Empty(EquipmentScope.ForCompany(machines, Guid.Empty));
        var specs = new[] {
            new EquipmentSpecMRO { SpecId = 10, EquipmentId = 1, Equipment = own },
            new EquipmentSpecMRO { SpecId = 20, EquipmentId = 2, Equipment = foreign }
        }.AsQueryable();
        Assert.Equal(10, Assert.Single(EquipmentScope.ForSpecifications(specs, company, 1, 10)).SpecId);
        Assert.Empty(EquipmentScope.ForSpecifications(specs, company, 2, 20));
        Assert.Empty(EquipmentScope.ForSpecifications(specs, company, 1, 20));
        Assert.Empty(EquipmentScope.ForSpecifications(specs, null, 1, 10));
    }

    [Fact]
    public void EfModel_MatchesAuditedDatabase_AndScopeTranslatesToSql()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var machine = db.Model.FindEntityType(typeof(EquipmentMRO))!;
        Assert.Equal("text", machine.FindProperty(nameof(EquipmentMRO.GroupType))!.GetColumnType());
        foreach (var name in new[] { nameof(EquipmentMRO.AreaId), nameof(EquipmentMRO.FactoryId), nameof(EquipmentMRO.PartId) })
        {
            Assert.False(machine.FindProperty(name)!.IsNullable);
            Assert.True(Assert.Single(machine.GetForeignKeys(), f => f.Properties.Any(p => p.Name == name)).IsRequired);
        }
        var details = db.Model.FindEntityType(typeof(EquipmentDetailMRO))!;
        Assert.True(Assert.Single(details.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(EquipmentMRO)).IsUnique);
        var sql = EquipmentScope.ForSpecifications(db.EquipmentSpecsMro, Guid.NewGuid(), 7, 8).ToQueryString();
        Assert.Contains("factory_id", sql);
        Assert.Contains("equipment_id", sql);
        Assert.Contains("spec_id", sql);
    }

    [Fact]
    public async Task PaginationOverflowAndInvalidInput_AreRejectedBeforeDbAccess()
    {
        var service = Service(new TestUser());
        Assert.Equal(400, (await service.GetListAsync(new() { Page = int.MaxValue, PageSize = 100 }, default)).StatusCode);
        Assert.Equal(400, (await service.SaveAsync(null, new(), default)).StatusCode);
        Assert.Equal(400, (await service.SaveSpecAsync(1, null, new() { SpecKey = "  " }, default)).StatusCode);
        var request = new SaveEquipmentRequest { EquipmentExternalId = "M1", EquipmentName = "Mixer", AreaId = 1, PartId = Guid.NewGuid(),
            Details = new() { PurchaseDate = new(2026, 2, 1), CommissioningDate = new(2026, 1, 1) } };
        Assert.Equal("invalidDates", EquipmentValidation.Validate(request));
    }

    private static EquipmentManagementService Service(TestUser user) => new(null!, user, new CurrentUserPermissionService(user));

    [Fact]
    public void PermissionCatalog_PreservesEquipmentClaimsForMigratedAndLegacyRoles()
    {
        var explicitPermissions = ApplicationPermissionCatalog.ResolveRole(ApplicationRoles.Admin, true,
            [ApplicationPermissions.Equipment.View, ApplicationPermissions.Equipment.Update]);
        Assert.Contains(ApplicationPermissions.Equipment.View, explicitPermissions);
        Assert.Contains(ApplicationPermissions.Equipment.Update, explicitPermissions);
        var fallback = ApplicationPermissionCatalog.ResolveRole(ApplicationRoles.Maintenance.MaintenanceUser, false, []);
        Assert.Contains(ApplicationPermissions.Equipment.Create, fallback);
        Assert.DoesNotContain(ApplicationPermissions.Equipment.Delete, fallback);
    }

    private sealed class TestUser : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public Guid? EmployeeId => null;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [ApplicationRoles.Admin];
        public IReadOnlyCollection<string> Permissions { get; init; } = [];
        public bool Explicit { get; init; }
        public bool HasExplicitPermissionSet => Explicit;
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
