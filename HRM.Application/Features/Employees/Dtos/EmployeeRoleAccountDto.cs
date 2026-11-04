namespace HRM.Application.Features.Employees.Dtos;

public sealed record EmployeeRoleAccountDto(
    Guid? EmployeeId, Guid UserId, string? FullName, string? UserName, string? Email);
