using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Administration;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Employees.Queries.GetEmployeeBasicInfoById
{
    internal sealed class GetEmployeeBasicInfoByIdQueryHandler
        : IRequestHandler<GetEmployeeBasicInfoByIdQuery, EmployeeBasicInfoDto?>
    {
        private readonly IEmployeeReadDbContext _dbContext;
        private readonly ICurrentUser _currentUser;

        public GetEmployeeBasicInfoByIdQueryHandler(
            IEmployeeReadDbContext dbContext,
            ICurrentUser currentUser)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
        }
        public async Task<EmployeeBasicInfoDto?> Handle(
            GetEmployeeBasicInfoByIdQuery request,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.Employees.AsNoTracking();
            if (!EmployeeAdministrationRules.CanManageAllCompanies(_currentUser))
            {
                var companyId = _currentUser.CompanyId
                    ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");
                query = query.Where(employee => employee.CompanyId == companyId);
            }

            return await query
                .Where(x => x.EmployeeId == request.EmployeeId)
                .Select(x => new EmployeeBasicInfoDto
                {
                    Id = x.EmployeeId,
                    ExternalId = x.ExternalId,
                    FullName = x.FullName
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
