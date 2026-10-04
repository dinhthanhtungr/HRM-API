namespace HRM.Application.Features.Employees.Administration;

internal static class EmployeeAccountLifecycleRules
{
    public static bool CanActivateAccount(bool employeeIsActive)
        => employeeIsActive;

    public static bool MustDisableAccount(bool employeeIsActive)
        => !employeeIsActive;

    public static bool IsValidEndDate(bool isActive, DateOnly? requestedEndDate, DateTime? dateHired, DateOnly today)
    {
        if (isActive) return requestedEndDate is null;
        var endDate = requestedEndDate ?? today;
        return endDate <= today && (!dateHired.HasValue || endDate >= DateOnly.FromDateTime(dateHired.Value));
    }
}
