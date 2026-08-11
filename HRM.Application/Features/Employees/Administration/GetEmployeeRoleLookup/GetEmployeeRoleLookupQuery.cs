using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Administration.GetEmployeeRoleLookup;

public sealed record GetEmployeeRoleLookupQuery
    : IRequest<EmployeeAdministrationResult<IReadOnlyList<EmployeeRoleLookupDto>>>;
