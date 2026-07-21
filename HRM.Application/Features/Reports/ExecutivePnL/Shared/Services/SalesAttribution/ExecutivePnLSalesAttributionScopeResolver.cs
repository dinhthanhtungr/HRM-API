using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Rules;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.SalesAttribution
{
    /// <summary>
    /// Đọc database để xác định customer nào được tính cho sale/group nào trong báo cáo Executive PnL.
    /// </summary>
    internal sealed class ExecutivePnLSalesAttributionScopeResolver
    {
        private static readonly string[] ReportableSaleRoles =
        {
            ApplicationRoles.Sales.SaleUser,
            ApplicationRoles.Admin,
            ApplicationRoles.President
        };

        private readonly IReportReadDbContext _dbContext;
        private readonly ICurrentUser _currentUser;
        private readonly string InternalCustomer =  ExecutivePnLReportRules.InternalCustomerExternalId;

        public ExecutivePnLSalesAttributionScopeResolver(IReportReadDbContext dbContext, ICurrentUser currentUser)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Tạo scope phân bổ customer theo điều kiện: customer có assignment, sale có role hợp lệ và thuộc group sale hợp lệ.
        /// </summary>
        public async Task<ExecutivePnLSalesAttributionScope> ResolveAsync(
            Guid? companyId,
            IReadOnlyCollection<Guid> customerIds,
            Guid? saleGroupId,
            Guid? salePersonId,
            CancellationToken cancellationToken)
        {
            var currentEmployeeId = _currentUser.EmployeeId;
            var hasFullAccess = (_currentUser.IsInRole(ApplicationRoles.Admin) && _currentUser.IsInRole(ApplicationRoles.Sales.SaleUser))
                || _currentUser.IsInRole(ApplicationRoles.President);

            if (!hasFullAccess && !currentEmployeeId.HasValue)
            {
                return EmptyScope();
            }

            var roleSalesQuery = _dbContext.UserRoles
                .AsNoTracking()
                .Where(x => x.IsActive
                    && x.User.EmployeeId.HasValue
                    && x.User.Employee != null
                    && x.User.Employee.IsActive
                    && ReportableSaleRoles.Contains(x.Role.Name!))
                .Where(x => !companyId.HasValue || x.User.Employee!.CompanyId == companyId.Value);

            // Đây là phân quyền thật từ token/currentUser
            if (!hasFullAccess)
            {
                roleSalesQuery = roleSalesQuery
                    .Where(x => x.User.EmployeeId == currentEmployeeId!.Value);
            }

            // Đây chỉ là filter thêm từ query
            if (salePersonId.HasValue)
            {
                roleSalesQuery = roleSalesQuery
                    .Where(x => x.User.EmployeeId == salePersonId.Value);
            }

            var roleSales = await roleSalesQuery
                .Select(x => new
                {
                    SaleId = x.User.EmployeeId!.Value,
                    SaleLabel = x.User.Employee!.FullName
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            var roleSaleIds = roleSales
                .Select(x => x.SaleId)
                .ToHashSet();

            if (roleSaleIds.Count == 0)
            {
                return EmptyScope();
            }

            var saleGroupQuery = _dbContext.MemberInGroups
                .AsNoTracking()
                .Where(x => x.IsActive
                    && x.Profile.HasValue
                    && roleSaleIds.Contains(x.Profile.Value)
                    && (!companyId.HasValue || x.Group.CompanyId == companyId.Value));

            // Đây cũng chỉ là filter thêm từ query
            if (saleGroupId.HasValue)
            {
                saleGroupQuery = saleGroupQuery
                    .Where(x => x.GroupId == saleGroupId.Value);
            }

            var saleGroupRows = await saleGroupQuery
                .Select(x => new
                {
                    SaleId = x.Profile!.Value,
                    GroupKey = x.Group.GroupId.ToString(),
                    GroupLabel = x.Group.Name ?? x.Group.ExternalId
                })
                .ToListAsync(cancellationToken);

            var groupBySale = saleGroupRows
                .GroupBy(x => x.SaleId)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderBy(g => g.GroupLabel).First());

            var reportableSales = roleSales
                .Where(x => groupBySale.ContainsKey(x.SaleId))
                .ToDictionary(x => x.SaleId, x => x.SaleLabel);

            if (reportableSales.Count == 0)
            {
                return EmptyScope();
            }

            var assignments = await _dbContext.CustomerAssignments
                .AsNoTracking()
                .Where(x => x.IsActive
                    && x.Customer.ExternalId != InternalCustomer
                    && x.Customer.IsActive != false
                    && customerIds.Contains(x.CustomerId)
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

                    return new ExecutivePnLSalesAttribution
                    {
                        CustomerId = x.CustomerId,
                        SaleId = x.SaleId,
                        SaleLabel = reportableSales[x.SaleId],
                        GroupKey = group.GroupKey,
                        GroupLabel = group.GroupLabel
                    };
                })
                .ToDictionary(x => x.CustomerId);

            return new ExecutivePnLSalesAttributionScope(attributions);
        }


        private static ExecutivePnLSalesAttributionScope EmptyScope()
        {
            return new ExecutivePnLSalesAttributionScope(
                new Dictionary<Guid, ExecutivePnLSalesAttribution>());
        }
    }
}

