using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Employees.Administration;
using HRM.Application.Features.Employees.Dtos;
using HRM.Domain.Enums.Employees;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Employees.Queries.GetEmployeeDropdown
{
    internal sealed class GetEmployeeLookupQueryHandler
        : IRequestHandler<GetEmployeeLookupQuery, PagedResult<EmployeeLookupDto>>
    {
        private readonly IEmployeeReadDbContext _dbContext;
        private readonly ICurrentUser _currentUser;

        public GetEmployeeLookupQueryHandler(
            IEmployeeReadDbContext dbContext,
            ICurrentUser currentUser)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
        }

        public async Task<PagedResult<EmployeeLookupDto>> Handle(
            GetEmployeeLookupQuery request,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.Employees
                .Where(x => x.IsActive && x.Status == EmployeeStatus.Active.ToString())
                .AsQueryable();

            if (!EmployeeAdministrationRules.CanManageAllCompanies(_currentUser))
            {
                var companyId = _currentUser.CompanyId
                    ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");
                query = query.Where(employee => employee.CompanyId == companyId);
            }

            var keyword = request.NormalizedKeyword ?? NormalizeSearch(request.Search);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    x.ExternalId.StartsWith(keyword) ||
                    x.FullName.Contains(keyword));
            }

            if (request.PartId.HasValue)
            {
                query = query.Where(x => x.PartId == request.PartId.Value);
            }

            if (request.GroupId.HasValue)
            {
                query = query.Where(x =>
                    _dbContext.MemberInGroups.Any(m =>
                        m.GroupId == request.GroupId.Value &&
                        m.Profile == x.EmployeeId &&
                        m.IsActive));
            }

            var projectedQuery = query
                .OrderBy(x => x.FullName)
                .Select(x => new EmployeeLookupDto
                {
                    EmployeeId = x.EmployeeId,
                    ExternalId = x.ExternalId,
                    FullName = x.FullName
                });

            return await projectedQuery.ToPagedResultAsync(
                request.NormalizedPageNumber,
                request.NormalizedPageSize,
                cancellationToken);
        }

        private static string? NormalizeSearch(string? search)
            => string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    }
}
