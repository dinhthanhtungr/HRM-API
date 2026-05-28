using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Employees.Dtos;
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

        public GetEmployeeLookupQueryHandler(IEmployeeReadDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PagedResult<EmployeeLookupDto>> Handle(
            GetEmployeeLookupQuery request,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.Employees
                .Where(x => x.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var keyword = request.Search.Trim();

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
    }
}
