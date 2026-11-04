using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Commons.Reporting.SalesAttribution
{
    /// <summary>
    /// Đọc database để xác định customer nào được tính cho sale/group nào trong báo cáo Executive PnL.
    /// </summary>
    internal sealed class SalesAttributionScopeResolver
    {
        private static readonly string[] ReportableSaleRoles =
        {
            ApplicationRoles.Sales.SaleUser,
            ApplicationRoles.Admin,
            ApplicationRoles.President
        };

        private readonly IReportReadDbContext _dbContext;
        private readonly ICurrentUser _currentUser;
        private readonly string InternalCustomer =  InternalCustomerRules.InternalCustomerExternalId;

        public SalesAttributionScopeResolver(IReportReadDbContext dbContext, ICurrentUser currentUser)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Tạo scope phân bổ customer theo điều kiện: customer có assignment, sale có role hợp lệ và thuộc group sale hợp lệ.
        /// </summary>
        public async Task<SalesAttributionScope> ResolveAsync(
            Guid? companyId,
            IReadOnlyCollection<Guid> customerIds,
            Guid? saleGroupId,
            Guid? salePersonId,
            CancellationToken cancellationToken,
            bool includeUnassignedCompanyCustomers = false)
        {
            if (!ReportCompanyScope.IsAllowed(_currentUser, companyId))
                return EmptyScope();
            companyId = _currentUser.CompanyId;
            var currentEmployeeId = _currentUser.EmployeeId;
            var hasFullAccess = _currentUser.IsInAnyRole(ApplicationRoleSets.Reports.SalesCompanyReaders);
            var includeUnassigned = includeUnassignedCompanyCustomers
                && !saleGroupId.HasValue && !salePersonId.HasValue;

            if (!hasFullAccess && !currentEmployeeId.HasValue)
            {
                return EmptyScope();
            }

            // Customer visibility and sales attribution are independent decisions.
            var customers = _dbContext.MerchandiseOrders.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.Customer != null && customerIds.Contains(x.CustomerId))
                .Select(x => x.Customer!);
            if (!hasFullAccess)
            {
                var leaderGroups = await _dbContext.MemberInGroups.AsNoTracking()
                    .Where(x => x.IsActive && x.IsAdmin == true && x.Profile == currentEmployeeId
                        && x.Group.CompanyId == companyId)
                    .Select(x => x.GroupId).Distinct().ToListAsync(cancellationToken);
                var viewer = new ViewerScope(companyId!.Value, currentEmployeeId!.Value,
                    _currentUser.IsInRole(ApplicationRoles.Admin)
                        || _currentUser.IsInRole(ApplicationRoles.Sales.CustomerViewAll),
                    false, leaderGroups.ToHashSet(), new HashSet<Guid> { currentEmployeeId.Value }, DateTime.Now);
                customers = CustomerVisibilityService.ApplyCustomerVisibilityCore(customers, viewer, includeInactive: false);
            }
            var visibleCustomerIds = await customers.Select(x => x.CustomerId).Distinct().ToListAsync(cancellationToken);

            var roleSalesQuery = _dbContext.UserRoles
                .AsNoTracking()
                .Where(x => x.IsActive
                    && x.User.EmployeeId.HasValue
                    && x.User.Employee != null
                    && x.User.Employee.IsActive
                    && ReportableSaleRoles.Contains(x.Role.Name!))
                .Where(x => !companyId.HasValue || x.User.Employee!.CompanyId == companyId.Value);

            // Đây chỉ là filter thêm từ query
            if (salePersonId.HasValue)
            {
                roleSalesQuery = roleSalesQuery
                    .Where(x => x.User.EmployeeId == salePersonId.Value);
            }

            var saleGroupQuery = _dbContext.MemberInGroups
                .AsNoTracking()
                .Where(x => x.IsActive
                    && x.Profile.HasValue
                    && (!companyId.HasValue || x.Group.CompanyId == companyId.Value));

            // Đây cũng chỉ là filter thêm từ query
            if (saleGroupId.HasValue)
            {
                saleGroupQuery = saleGroupQuery
                    .Where(x => x.GroupId == saleGroupId.Value);
            }

            var saleGroupRows = await (
                from role in roleSalesQuery
                join member in saleGroupQuery on role.User.EmployeeId equals member.Profile
                select new
                {
                    SaleId = member.Profile!.Value,
                    SaleLabel = role.User.Employee!.FullName,
                    GroupKey = member.Group.GroupId.ToString(),
                    GroupLabel = member.Group.Name ?? member.Group.ExternalId
                }).Distinct()
                .ToListAsync(cancellationToken);

            var groupBySale = saleGroupRows
                .GroupBy(x => x.SaleId)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderBy(g => g.GroupLabel).First());

            var reportableSales = groupBySale.ToDictionary(x => x.Key, x => x.Value.SaleLabel);

            if (reportableSales.Count == 0 && !includeUnassigned)
            {
                return EmptyScope();
            }

            var assignments = await _dbContext.CustomerAssignments
                .AsNoTracking()
                .Where(x => x.IsActive
                    && x.Customer.ExternalId != InternalCustomer
                    && x.Customer.IsActive != false
                    && visibleCustomerIds.Contains(x.CustomerId)
                    && reportableSales.Keys.Contains(x.EmployeeId)
                    && (!companyId.HasValue || x.CompanyId == companyId.Value))
                .Select(x => new
                {
                    x.CustomerId,
                    SaleId = x.EmployeeId,
                    x.UpdatedDate,
                    x.CreatedDate
                })
                .ToListAsync(cancellationToken);

            var attributions = assignments
                .GroupBy(x => x.CustomerId)
                .Select(x => x
                    .OrderByDescending(a => a.UpdatedDate)
                    .ThenByDescending(a => a.CreatedDate)
                    .First())
                .Where(x => reportableSales.ContainsKey(x.SaleId))
                .Select(x =>
                {
                    var group = groupBySale[x.SaleId];

                    return new SalesAttribution
                    {
                        CustomerId = x.CustomerId,
                        SaleId = x.SaleId,
                        SaleLabel = reportableSales[x.SaleId],
                        GroupKey = group.GroupKey,
                        GroupLabel = group.GroupLabel
                    };
                })
                .ToDictionary(x => x.CustomerId);

            if (includeUnassigned)
            {
                // Tổng công ty giữ doanh số lịch sử dù assignment/sale hiện tại không còn hợp lệ.
                var companyCustomers = await _dbContext.MerchandiseOrders.AsNoTracking()
                    .Where(x => x.CompanyId == companyId && visibleCustomerIds.Contains(x.CustomerId)
                        && x.Customer != null && x.Customer.ExternalId != InternalCustomer)
                    .Select(x => x.CustomerId).Distinct().ToListAsync(cancellationToken);
                foreach (var customerId in companyCustomers)
                    attributions.TryAdd(customerId, new SalesAttribution
                    {
                        CustomerId = customerId, SaleId = Guid.Empty,
                        SaleLabel = "Chưa phân bổ sale", GroupKey = "UNASSIGNED",
                        GroupLabel = "Chưa phân bổ sale"
                    });
            }

            return new SalesAttributionScope(attributions);
        }


        private static SalesAttributionScope EmptyScope()
        {
            return new SalesAttributionScope(
                new Dictionary<Guid, SalesAttribution>());
        }
    }
}

