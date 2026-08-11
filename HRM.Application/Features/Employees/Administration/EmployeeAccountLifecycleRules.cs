namespace HRM.Application.Features.Employees.Administration;

internal static class EmployeeAccountLifecycleRules
{
    public static bool CanActivateAccount(bool employeeIsActive)
        => employeeIsActive;

    public static bool MustDisableAccount(bool employeeIsActive)
        => !employeeIsActive;
}
