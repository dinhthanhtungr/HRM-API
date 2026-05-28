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

namespace HRM.Application.Features.Employees.Queries.GetEmployeePageQuery
{
    internal sealed class GetEmployeePageQueryHandler
        : IRequestHandler<GetEmployeePageQuery, PagedResult<EmployeePageDto>>
    {
        private readonly IEmployeeReadDbContext _dbContext;

        public GetEmployeePageQueryHandler(IEmployeeReadDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PagedResult<EmployeePageDto>> Handle(
            GetEmployeePageQuery request, 
            CancellationToken cancellationToken)
        {
            var query = _dbContext.Employees
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                var keyword = request.Keyword.Trim();

                query = query.Where(x =>
                    x.ExternalId.StartsWith(keyword) ||
                    x.FullName.Contains(keyword));
            }

            if (!string.IsNullOrEmpty(request.Status))
            {
                query = query.Where(e => e.Status == request.Status);
            }

            if (request.PartId.HasValue)
            {
                query = query.Where(e => e.PartId == request.PartId.Value);
            }

            if (request.GroupId.HasValue)
            {
                query = query.Where(x =>
                    _dbContext.MemberInGroups.Any(m =>
                        m.GroupId == request.GroupId.Value &&
                        m.Profile == x.EmployeeId &&
                        m.IsActive));
            }

            var isDescending = string.Equals(
                request.SortDirection,
                "desc",
                StringComparison.OrdinalIgnoreCase);

            query = request.SortBy?.ToLowerInvariant() switch
            {
                "externalid" or "external_id" => isDescending
                    ? query.OrderByDescending(x => x.ExternalId)
                    : query.OrderBy(x => x.ExternalId),

                "fullname" or "full_name" => isDescending
                    ? query.OrderByDescending(x => x.FullName)
                    : query.OrderBy(x => x.FullName),

                "createddate" or "created_date" => isDescending
                    ? query.OrderByDescending(x => x.CreatedDate)
                    : query.OrderBy(x => x.CreatedDate),

                _ => query.OrderByDescending(x => x.CreatedDate)
                    .ThenBy(x => x.ExternalId)
            };

            var projectQuery = query
                .Select(e => new EmployeePageDto
                {
                    EmployeeId = e.EmployeeId,
                    ExternalId = e.ExternalId,
                    FullName = e.FullName,
                    PartName = e.Part != null ? e.Part.PartName : string.Empty,
                    PositionName = e.EmployeeWorkProfiles
                        .Where(w => w.IsCurrent)
                        .OrderByDescending(w => w.EffectiveFrom)
                        .Select(w => w.JobTitle != null ? w.JobTitle.Name : string.Empty)
                        .FirstOrDefault() ?? string.Empty,
                    IsActive = e.IsActive
                });

            return await projectQuery
                .ToPagedResultAsync(
                    request.NormalizedPageNumber,
                    request.NormalizedPageSize,
                    cancellationToken);
        }
    }
}
