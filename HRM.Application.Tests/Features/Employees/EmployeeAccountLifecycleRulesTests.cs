using HRM.Application.Abstractions.Identity;
using HRM.Application.Features.Employees.Administration;
using HRM.Application.Features.Employees.Administration.GetEmployeeAccountPermissions;

namespace HRM.Application.Tests.Features.Employees;

public sealed class EmployeeAccountLifecycleRulesTests
{
    [Fact]
    public void Active_account_can_only_be_enabled_for_active_employee()
    {
        Assert.True(EmployeeAccountLifecycleRules.CanActivateAccount(employeeIsActive: true));
        Assert.False(EmployeeAccountLifecycleRules.CanActivateAccount(employeeIsActive: false));
    }

    [Fact]
    public void Inactive_employee_requires_linked_account_to_be_disabled()
    {
        Assert.True(EmployeeAccountLifecycleRules.MustDisableAccount(employeeIsActive: false));
        Assert.False(EmployeeAccountLifecycleRules.MustDisableAccount(employeeIsActive: true));
    }

    [Fact]
    public void Account_permissions_mapping_exposes_both_lifecycle_states()
    {
        var employeeId = Guid.NewGuid();
        var account = new EmployeeIdentityAccount(
            Guid.NewGuid(),
            "employee01",
            "employee01@example.com",
            false,
            ["SaleUser"]);

        var dto = GetEmployeeAccountPermissionsQueryHandler.MapAccount(
            employeeId,
            employeeIsActive: true,
            account);

        Assert.Equal(employeeId, dto.EmployeeId);
        Assert.True(dto.EmployeeIsActive);
        Assert.True(dto.HasAccount);
        Assert.False(dto.AccountIsActive);
        Assert.Equal(["SaleUser"], dto.Roles);
    }
}
