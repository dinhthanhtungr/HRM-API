using HRM.Application.Abstractions.Identity;
using HRM.Application.Features.Employees.Administration;

namespace HRM.Application.Tests.Features.Employees;

public sealed class EmployeeAccountLifecycleRulesTests
{
    [Fact]
    public void Employment_end_date_cannot_be_future_or_before_hire_and_is_absent_on_reactivation()
    {
        var today = new DateOnly(2026, 10, 4);
        var hired = new DateTime(2026, 9, 1);
        Assert.False(EmployeeAccountLifecycleRules.IsValidEndDate(false, today.AddDays(1), hired, today));
        Assert.False(EmployeeAccountLifecycleRules.IsValidEndDate(false, new DateOnly(2026, 8, 31), hired, today));
        Assert.True(EmployeeAccountLifecycleRules.IsValidEndDate(false, new DateOnly(2026, 9, 1), hired, today));
        Assert.True(EmployeeAccountLifecycleRules.IsValidEndDate(false, null, hired, today));
        Assert.True(EmployeeAccountLifecycleRules.IsValidEndDate(true, null, hired, today));
        Assert.False(EmployeeAccountLifecycleRules.IsValidEndDate(true, today, hired, today));
    }

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

        var dto = EmployeeAccountPermissionsMapper.Map(
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
