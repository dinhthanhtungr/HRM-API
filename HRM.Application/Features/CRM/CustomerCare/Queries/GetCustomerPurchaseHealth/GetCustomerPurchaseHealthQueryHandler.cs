using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerPurchaseHealth;

internal sealed class GetCustomerPurchaseHealthQueryHandler
    : IRequestHandler<GetCustomerPurchaseHealthQuery, OperationResult<CustomerPurchaseHealthReportDto>>
{
    private const int MaxInactivePurchaseDays = 3650;
    private readonly ICRMReadDbContext _dbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetCustomerPurchaseHealthQueryHandler(
        ICRMReadDbContext dbContext,
        CustomerCrmAccessService accessService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _accessService = accessService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<CustomerPurchaseHealthReportDto>> Handle(
        GetCustomerPurchaseHealthQuery request,
        CancellationToken cancellationToken)
    {
        var query = request.Query;
        if (!Enum.IsDefined(query.PurchaseStatus))
        {
            return OperationResult<CustomerPurchaseHealthReportDto>.Fail("Purchase status is invalid.");
        }

        if (query.InactivePurchaseDays is < 1 or > MaxInactivePurchaseDays)
        {
            return OperationResult<CustomerPurchaseHealthReportDto>.Fail(
                $"Inactive purchase days must be between 1 and {MaxInactivePurchaseDays}.");
        }

        var lastPurchaseRange = ResolveLastPurchaseRange(query);
        if (!lastPurchaseRange.IsValid)
        {
            return OperationResult<CustomerPurchaseHealthReportDto>.Fail(lastPurchaseRange.Error!);
        }

        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var employee = await _accessService.ResolveEmployeeAsync(
            scope,
            query.AssignedSaleEmployeeId,
            query.OnlyMine,
            cancellationToken);
        if (!employee.IsAllowed)
        {
            return OperationResult<CustomerPurchaseHealthReportDto>.Fail("Employee is outside your scope.");
        }

        var group = await ResolveGroupMemberEmployeeIdsAsync(scope, query.GroupId, cancellationToken);
        if (!group.IsAllowed)
        {
            return OperationResult<CustomerPurchaseHealthReportDto>.Fail("Group is outside your scope.");
        }

        var now = scope.Now;
        var leaderGroupIds = scope.LeaderGroupIds.ToArray();
        var restrictAssignmentsToLeaderGroups = !scope.HasFullCustomerView && leaderGroupIds.Length > 0;
        var customersQuery = _accessService.VisibleCustomers(scope);

        if (query.CustomerId.HasValue)
        {
            if (query.CustomerId.Value == Guid.Empty)
            {
                return OperationResult<CustomerPurchaseHealthReportDto>.Fail("Customer id is invalid.");
            }

            customersQuery = customersQuery.Where(x => x.CustomerId == query.CustomerId.Value);
        }

        if (employee.EmployeeId.HasValue)
        {
            var employeeId = employee.EmployeeId.Value;
            customersQuery = customersQuery.Where(x =>
                x.CurrentSaleId == employeeId ||
                x.CustomerAssignments.Any(a => a.IsActive && a.EmployeeId == employeeId) ||
                x.CustomerClaims.Any(cl =>
                    cl.EmployeeId == employeeId &&
                    cl.IsActive &&
                    cl.Type == ClaimType.Work &&
                    cl.ExpiresAt > now));
        }

        if (group.EmployeeIds is { } groupEmployeeIds)
        {
            customersQuery = customersQuery.Where(x =>
                groupEmployeeIds.Contains(x.CurrentSaleId ?? Guid.Empty) ||
                x.CustomerAssignments.Any(a => a.IsActive && groupEmployeeIds.Contains(a.EmployeeId)) ||
                x.CustomerClaims.Any(cl =>
                    cl.IsActive &&
                    cl.Type == ClaimType.Work &&
                    cl.ExpiresAt > now &&
                    groupEmployeeIds.Contains(cl.EmployeeId)));
        }

        if (query.NormalizedKeyword is { } keyword)
        {
            customersQuery = customersQuery.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.CustomerName.Contains(keyword));
        }

        var customers = await customersQuery
            .Select(x => new CustomerPurchaseCustomer
            {
                CustomerId = x.CustomerId,
                CustomerExternalId = x.ExternalId,
                CustomerName = x.CustomerName,
                AssignedSaleEmployeeId = x.IsLead && !x.CustomerAssignments.Any(a => a.IsActive)
                    ? x.CustomerClaims
                        .Where(cl => cl.IsActive && cl.Type == ClaimType.Work && cl.ExpiresAt > now)
                        .OrderByDescending(cl => cl.ExpiresAt)
                        .Select(cl => (Guid?)cl.EmployeeId)
                        .FirstOrDefault() ?? x.CurrentSaleId
                    : x.CustomerAssignments
                        .Where(a => a.IsActive && (!restrictAssignmentsToLeaderGroups || leaderGroupIds.Contains(a.GroupId)))
                        .OrderByDescending(a => a.CreatedDate)
                        .Select(a => (Guid?)a.EmployeeId)
                        .FirstOrDefault() ?? x.CurrentSaleId,
                AssignedSaleEmployeeName = x.IsLead && !x.CustomerAssignments.Any(a => a.IsActive)
                    ? x.CustomerClaims
                        .Where(cl => cl.IsActive && cl.Type == ClaimType.Work && cl.ExpiresAt > now)
                        .OrderByDescending(cl => cl.ExpiresAt)
                        .Select(cl => cl.Employee.FullName)
                        .FirstOrDefault() ?? _dbContext.Employees
                            .Where(employee => x.CurrentSaleId.HasValue && employee.EmployeeId == x.CurrentSaleId.Value)
                            .Select(employee => employee.FullName)
                            .FirstOrDefault()
                    : x.CustomerAssignments
                        .Where(a => a.IsActive && (!restrictAssignmentsToLeaderGroups || leaderGroupIds.Contains(a.GroupId)))
                        .OrderByDescending(a => a.CreatedDate)
                        .Select(a => a.Employee.FullName)
                        .FirstOrDefault() ?? _dbContext.Employees
                            .Where(employee => x.CurrentSaleId.HasValue && employee.EmployeeId == x.CurrentSaleId.Value)
                            .Select(employee => employee.FullName)
                            .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var customerIds = customers.Select(x => x.CustomerId).ToArray();
        var today = _dateTimeProvider.Now.Date;
        var ordersByCustomer = await _dbContext.MerchandiseOrders
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == scope.CompanyId &&
                x.IsActive &&
                x.OrderType == OrderType.Merchandise &&
                x.CustomerExternalIdSnapshot != InternalCustomerRules.InternalCustomerExternalId &&
                !CustomerPurchaseHealthRules.ExcludedOrderStatuses.Contains(x.Status) &&
                customerIds.Contains(x.CustomerId))
            .GroupBy(x => x.CustomerId)
            .Select(grouping => new CustomerPurchaseOrder
            {
                CustomerId = grouping.Key,
                LifetimeOrderAmount = grouping.Sum(x =>
                    x.Currency == null ||
                    x.Currency.Trim() == string.Empty ||
                    x.Currency.ToUpper() == "VND" ||
                    !x.ExchangeRate.HasValue ||
                    x.ExchangeRate.Value <= 0
                        ? x.TotalPrice ?? 0m
                        : (x.TotalPrice ?? 0m) * x.ExchangeRate.Value),
                LastPurchaseDate = grouping.Max(x => x.CreateDate)
            })
            .ToDictionaryAsync(x => x.CustomerId, x => x, cancellationToken);

        var dormantBefore = today.AddDays(-query.InactivePurchaseDays);
        var allRows = customers.Select(customer => BuildRow(
                customer,
                ordersByCustomer.GetValueOrDefault(customer.CustomerId),
                today,
                dormantBefore))
            .ToList();
        var rows = allRows
            .Where(row => MatchesPurchaseStatus(row, query.PurchaseStatus))
            .Where(row => !lastPurchaseRange.From.HasValue || row.LastPurchaseDate >= lastPurchaseRange.From.Value)
            .Where(row => !lastPurchaseRange.ToExclusive.HasValue || row.LastPurchaseDate < lastPurchaseRange.ToExclusive.Value)
            .ToList();

        var orderedRows = ApplySorting(rows, query)
            .Skip((query.NormalizedPageNumber - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToList();

        return OperationResult<CustomerPurchaseHealthReportDto>.Ok(new CustomerPurchaseHealthReportDto
        {
            Header = new CustomerPurchaseHealthHeaderDto
            {
                VisibleCustomerCount = allRows.Count,
                HasPurchasedCount = allRows.Count(x => x.PurchaseStatus is CustomerPurchaseStatus.HasPurchased or CustomerPurchaseStatus.Dormant),
                NeverPurchasedCount = allRows.Count(x => x.PurchaseStatus == CustomerPurchaseStatus.NeverPurchased),
                DormantCustomerCount = allRows.Count(x => x.PurchaseStatus == CustomerPurchaseStatus.Dormant),
                InactivePurchaseDays = query.InactivePurchaseDays
            },
            Customers = new PagedResult<CustomerPurchaseHealthRowDto>(
                orderedRows,
                rows.Count,
                query.NormalizedPageNumber,
                query.NormalizedPageSize)
        });
    }

    private static CustomerPurchaseHealthRowDto BuildRow(
        CustomerPurchaseCustomer customer,
        CustomerPurchaseOrder? order,
        DateTime today,
        DateTime dormantBefore)
    {
        var hasPurchased = order is not null;
        var lastPurchaseDate = order?.LastPurchaseDate.Date;
        var isDormant = hasPurchased && lastPurchaseDate < dormantBefore;
        return new CustomerPurchaseHealthRowDto
        {
            CustomerId = customer.CustomerId,
            CustomerExternalId = customer.CustomerExternalId,
            CustomerName = customer.CustomerName,
            AssignedSaleEmployeeId = customer.AssignedSaleEmployeeId,
            AssignedSaleEmployeeName = customer.AssignedSaleEmployeeName,
            LifetimeOrderAmount = order?.LifetimeOrderAmount ?? 0m,
            LastPurchaseDate = lastPurchaseDate,
            DaysSinceLastPurchase = lastPurchaseDate.HasValue ? (today - lastPurchaseDate.Value).Days : null,
            PurchaseStatus = !hasPurchased
                ? CustomerPurchaseStatus.NeverPurchased
                : isDormant
                    ? CustomerPurchaseStatus.Dormant
                    : CustomerPurchaseStatus.HasPurchased
        };
    }

    private static bool MatchesPurchaseStatus(CustomerPurchaseHealthRowDto row, CustomerPurchaseStatus requestedStatus)
        => requestedStatus == CustomerPurchaseStatus.All || row.PurchaseStatus == requestedStatus;

    private static IOrderedEnumerable<CustomerPurchaseHealthRowDto> ApplySorting(
        IEnumerable<CustomerPurchaseHealthRowDto> rows,
        CustomerPurchaseHealthQuery query)
    {
        var descending = query.SortDescending;
        return query.NormalizedSortBy?.ToLowerInvariant() switch
        {
            CustomerPurchaseHealthSortFields.DaysSinceLastPurchase => descending
                ? rows.OrderByDescending(x => x.DaysSinceLastPurchase).ThenBy(x => x.CustomerName)
                : rows.OrderBy(x => x.DaysSinceLastPurchase).ThenBy(x => x.CustomerName),
            CustomerPurchaseHealthSortFields.LastPurchaseDate => descending
                ? rows.OrderByDescending(x => x.LastPurchaseDate).ThenBy(x => x.CustomerName)
                : rows.OrderBy(x => x.LastPurchaseDate).ThenBy(x => x.CustomerName),
            CustomerPurchaseHealthSortFields.LifetimeOrderAmount => descending
                ? rows.OrderByDescending(x => x.LifetimeOrderAmount).ThenBy(x => x.CustomerName)
                : rows.OrderBy(x => x.LifetimeOrderAmount).ThenBy(x => x.CustomerName),
            CustomerPurchaseHealthSortFields.CustomerName => descending
                ? rows.OrderByDescending(x => x.CustomerName).ThenBy(x => x.CustomerId)
                : rows.OrderBy(x => x.CustomerName).ThenBy(x => x.CustomerId),
            _ => rows.OrderByDescending(x => x.DaysSinceLastPurchase.HasValue)
                .ThenByDescending(x => x.DaysSinceLastPurchase)
                .ThenBy(x => x.CustomerName)
                .ThenBy(x => x.CustomerId)
        };
    }

    private async Task<GroupResolution> ResolveGroupMemberEmployeeIdsAsync(
        ViewerScope scope,
        Guid? requestedGroupId,
        CancellationToken cancellationToken)
    {
        if (!requestedGroupId.HasValue)
        {
            return GroupResolution.Allowed(null);
        }

        if (requestedGroupId.Value == Guid.Empty ||
            !scope.HasFullCustomerView && !scope.LeaderGroupIds.Contains(requestedGroupId.Value))
        {
            return GroupResolution.Denied();
        }

        var groupExists = await _dbContext.Groups.AsNoTracking().AnyAsync(x =>
            x.GroupId == requestedGroupId.Value && x.CompanyId == scope.CompanyId,
            cancellationToken);
        if (!groupExists)
        {
            return GroupResolution.Denied();
        }

        var employeeIds = await _dbContext.MemberInGroups.AsNoTracking()
            .Where(x =>
                x.GroupId == requestedGroupId.Value &&
                x.IsActive &&
                x.Profile.HasValue &&
                x.ProfileNavigation != null &&
                x.ProfileNavigation.CompanyId == scope.CompanyId &&
                x.ProfileNavigation.IsActive)
            .Select(x => x.Profile!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return GroupResolution.Allowed(employeeIds);
    }

    private static LastPurchaseRange ResolveLastPurchaseRange(CustomerPurchaseHealthQuery query)
    {
        if (!query.LastPurchaseFrom.HasValue && !query.LastPurchaseTo.HasValue)
        {
            return LastPurchaseRange.Valid(null, null);
        }

        var from = query.LastPurchaseFrom?.Date;
        var toExclusive = query.LastPurchaseTo?.Date.AddDays(1);
        if (from.HasValue && toExclusive.HasValue && toExclusive <= from)
        {
            return LastPurchaseRange.Invalid("Last purchase to date must be on or after from date.");
        }

        return LastPurchaseRange.Valid(from, toExclusive);
    }

    private sealed class CustomerPurchaseCustomer
    {
        public Guid CustomerId { get; init; }
        public string CustomerExternalId { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public Guid? AssignedSaleEmployeeId { get; init; }
        public string? AssignedSaleEmployeeName { get; init; }
    }

    private sealed class CustomerPurchaseOrder
    {
        public Guid CustomerId { get; init; }
        public decimal LifetimeOrderAmount { get; init; }
        public DateTime LastPurchaseDate { get; init; }
    }

    private sealed record GroupResolution(bool IsAllowed, Guid[]? EmployeeIds)
    {
        public static GroupResolution Allowed(Guid[]? employeeIds) => new(true, employeeIds);
        public static GroupResolution Denied() => new(false, null);
    }

    private sealed record LastPurchaseRange(bool IsValid, DateTime? From, DateTime? ToExclusive, string? Error)
    {
        public static LastPurchaseRange Valid(DateTime? from, DateTime? toExclusive) => new(true, from, toExclusive, null);
        public static LastPurchaseRange Invalid(string error) => new(false, null, null, error);
    }
}
