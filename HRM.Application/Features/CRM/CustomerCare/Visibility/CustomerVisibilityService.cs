using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.CustomerCare.Visibility
{
    internal sealed class CustomerVisibilityService : ICustomerVisibilityService
    {
        private readonly ICustomerVisibilityReadDbContext _dbContext;
        private readonly ICurrentUser _currentUser;
        private readonly IDateTimeProvider _dateTimeProvider;

        public CustomerVisibilityService(
            ICustomerVisibilityReadDbContext dbContext,
            ICurrentUser currentUser,
            IDateTimeProvider dateTimeProvider)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<ViewerScope> BuildScopeAsync(CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated)
            {
                throw new UnauthorizedAccessException();
            }

            var employeeId = _currentUser.EmployeeId
                ?? throw new UnauthorizedAccessException("Current user has no EmployeeId.");

            var companyId = _currentUser.CompanyId
                ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");

            var hasFullView =
                _currentUser.IsInRole(ApplicationRoles.Admin) ||
                _currentUser.IsInRole(ApplicationRoles.President) ||
                _currentUser.IsInRole(ApplicationRoles.Developer) ||
                _currentUser.IsInRole(ApplicationRoles.Sales.CustomerViewAll) ||
                _currentUser.IsInRole(ApplicationRoles.Lab.LabUser);

            var leaderGroupIds = await _dbContext.MemberInGroups
                .AsNoTracking()
                .Where(x =>
                    x.Profile == employeeId &&
                    x.IsAdmin == true &&
                    x.IsActive == true)
                .Select(x => x.GroupId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var employeeIdsInScope = new HashSet<Guid> { employeeId };

            if (leaderGroupIds.Count > 0)
            {
                var memberIds = await _dbContext.MemberInGroups
                    .AsNoTracking()
                    .Where(x =>
                        leaderGroupIds.Contains(x.GroupId) &&
                        x.IsActive == true &&
                        x.Profile.HasValue)
                    .Select(x => x.Profile!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var memberId in memberIds)
                {
                    employeeIdsInScope.Add(memberId);
                }
            }

            return new ViewerScope(
                CompanyId: companyId,
                EmployeeId: employeeId,
                HasFullCustomerView: hasFullView,
                LeaderGroupIds: leaderGroupIds.ToHashSet(),
                EmployeeIdsInScope: employeeIdsInScope,
                Now: _dateTimeProvider.Now);
        }

        public IQueryable<Customer> ApplyCustomerVisibility(
            IQueryable<Customer> query,
            ViewerScope scope)
        {
            query = query.Where(x =>
                x.CompanyId == scope.CompanyId &&
                x.IsActive == true);

            if (scope.HasFullCustomerView)
            {
                return query;
            }

            var now = scope.Now;
            var employeeId = scope.EmployeeId;
            var leaderGroupIds = scope.LeaderGroupIds.ToArray();

            query = ExcludeRestrictedCustomer(query);   


            return query.Where(c =>
                (
                    c.IsLead &&
                    (
                        !c.CustomerClaims.Any(cl =>
                            cl.IsActive &&
                            cl.Type == ClaimType.Work &&
                            cl.ExpiresAt > now)
                        ||
                        c.CustomerClaims.Any(cl =>
                            cl.IsActive &&
                            cl.Type == ClaimType.Work &&
                            cl.ExpiresAt > now &&
                            cl.EmployeeId == employeeId)
                        ||
                        c.CustomerClaims.Any(cl =>
                            cl.IsActive &&
                            cl.Type == ClaimType.Work &&
                            cl.ExpiresAt > now &&
                            leaderGroupIds.Contains(cl.GroupId))
                    )
                )
                ||
                (
                    !c.IsLead &&
                    (
                        c.CustomerAssignments.Any(a =>
                            a.IsActive &&
                            a.EmployeeId == employeeId)
                        ||
                        c.CustomerAssignments.Any(a =>
                            a.IsActive &&
                            leaderGroupIds.Contains(a.GroupId))
                        ||
                        c.CustomerClaims.Any(cl =>
                            cl.IsActive &&
                            cl.Type == ClaimType.Work &&
                            cl.ExpiresAt > now &&
                            cl.EmployeeId == employeeId)
                        ||
                        c.CustomerClaims.Any(cl =>
                            cl.IsActive &&
                            cl.Type == ClaimType.Work &&
                            cl.ExpiresAt > now &&
                            leaderGroupIds.Contains(cl.GroupId))
                    )
                ));
        }

        public IQueryable<SampleRequest> ApplySampleRequestVisibility(
            IQueryable<SampleRequest> query,
            IQueryable<Customer> customerQuery,
            ViewerScope scope)
        {
            query = query.Where(x =>
                x.CompanyId == scope.CompanyId &&
                x.IsActive == true);

            if (scope.HasFullCustomerView)
            {
                return query;
            }

            var visibleCustomerIds = ApplyCustomerVisibility(customerQuery, scope)
                .Select(x => x.CustomerId);

            return query.Where(x => visibleCustomerIds.Contains(x.CustomerId));
        }

        public IQueryable<MerchandiseOrder> ApplyMerchandiseOrderVisibility(
            IQueryable<MerchandiseOrder> query,
            IQueryable<Customer> customerQuery,
            ViewerScope scope)
        {
            query = query.Where(x =>
                x.CompanyId == scope.CompanyId &&
                x.IsActive == true);

            if (scope.HasFullCustomerView)
            {
                return query;
            }

            var visibleCustomerIds = ApplyCustomerVisibility(customerQuery, scope)
                .Select(x => x.CustomerId);

            return query.Where(x => visibleCustomerIds.Contains(x.CustomerId));
        }

        public IQueryable<Quotation> ApplyQuotationVisibility(
            IQueryable<Quotation> query,
            IQueryable<Customer> customerQuery,
            ViewerScope scope)
        {
            var visibleCustomerIds = ApplyCustomerVisibility(customerQuery, scope)
                .Select(x => x.CustomerId);

            return query.Where(x =>
                x.CompanyId == scope.CompanyId &&
                x.IsActive &&
                visibleCustomerIds.Contains(x.CustomerId));
        }


        // =========================== Helper Methods ===========================
        private IQueryable<Customer> ExcludeRestrictedCustomer(
            IQueryable<Customer> query)
        {
            query = query.Where(x => x.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId);
            return query;
        }

    }
}
