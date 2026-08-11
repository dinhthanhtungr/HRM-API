using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Administration.GetEmployeeAccountPermissions;

/// <summary>
/// Trả tài khoản và các role active của nhân viên trong company scope được quản lý.
/// </summary>
public sealed record GetEmployeeAccountPermissionsQuery(Guid EmployeeId)
    : IRequest<EmployeeAdministrationResult<EmployeeAccountPermissionsDto>>;
