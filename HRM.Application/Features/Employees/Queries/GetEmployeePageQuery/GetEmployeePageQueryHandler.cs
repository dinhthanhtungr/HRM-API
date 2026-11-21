using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Employees.Administration;
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
        private readonly ICurrentUser _currentUser;

        public GetEmployeePageQueryHandler(
            IEmployeeReadDbContext dbContext,
            ICurrentUser currentUser)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
        }

        public async Task<PagedResult<EmployeePageDto>> Handle(
            GetEmployeePageQuery request, 
            CancellationToken cancellationToken)
        {
            var query = _dbContext.Employees
                .AsNoTracking()
                .AsQueryable();

            if (!EmployeeAdministrationRules.CanManageAllCompanies(_currentUser))
            {
                var companyId = _currentUser.CompanyId
                    ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");
                query = query.Where(employee => employee.CompanyId == companyId);
            }

            var smartSearch = HRM.Application.Commons.Searching.SmartSearch.For(query, fields => [
                fields.Code("employeeCode", x => x.ExternalId, "Mã nhân viên", "Employee code"),
                fields.Name("employeeName", x => x.FullName, "Nhân viên", "Employee"),
                fields.Name("departmentName", x => x.Part == null ? "" : x.Part.PartName, "Phòng ban", "Department"),
                fields.Names("jobTitle", x => x.EmployeeWorkProfiles.Where(w => w.IsCurrent),
                    w => w.JobTitle == null ? "" : w.JobTitle.Name, "Chức danh", "Job title")
            ]);
            query = smartSearch.ApplyRequest(query, request.Keyword, request, out var smartKeyword);
            if (!string.IsNullOrWhiteSpace(smartKeyword))
            {
                var keyword = smartKeyword.Trim();

                query = query.Where(x =>
                    x.ExternalId.StartsWith(keyword) ||
                    EF.Functions.ILike(x.FullName, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter));
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

            if (await smartSearch.SuggestionsAsync<EmployeePageDto>(query, request, cancellationToken) is { } suggestions)
                return suggestions;
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
