using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.CustomerCare.Visibility;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;

/// <summary>Kế hoạch PO theo ngày hứa MFG muộn nhất; giữ đơn đã giao đúng ngày và đơn quá hạn còn thiếu.</summary>
internal sealed class GetDeliveryPlanQueryHandler(
    IDispatchReadDbContext db, ICurrentUser user, IDateTimeProvider clock, DeliveryPlanReadModel readModel,
    ICustomerVisibilityService customerVisibility, ICustomerVisibilityReadDbContext customerDb)
    : IRequestHandler<GetDeliveryPlanQuery, OperationResult<DeliveryPlanResponseDto>>
{
    public async Task<OperationResult<DeliveryPlanResponseDto>> Handle(GetDeliveryPlanQuery request, CancellationToken ct)
    {
        if (!DeliveryOrderAccessRules.CanRead(user) || user.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<DeliveryPlanResponseDto>.Fail("Bạn không có quyền hoặc chưa có thông tin công ty.");
        var date = request.Date ?? Tomorrow(clock.Now);
        if (date == DateOnly.MaxValue || request.Progress.HasValue && !Enum.IsDefined(request.Progress.Value) ||
            request.NormalizedKeyword?.Length > 200 || request.NormalizedPageNumber > int.MaxValue / request.NormalizedPageSize)
            return OperationResult<DeliveryPlanResponseDto>.Fail("Ngày, trạng thái hoặc phân trang kế hoạch không hợp lệ.");

        var orders = db.MerchandiseOrders.AsNoTracking();
        if (DeliveryOrderAccessRules.RequiresCustomerScopeForPlan(user))
        {
            if (user.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
                return OperationResult<DeliveryPlanResponseDto>.Fail("Không xác định được nhân viên để giới hạn kế hoạch giao hàng.");
            var scope = await customerVisibility.BuildReadScopeAsync(ct);
            if (scope.CompanyId != companyId || scope.EmployeeId != employeeId)
                return OperationResult<DeliveryPlanResponseDto>.Fail("Phạm vi khách hàng không hợp lệ.");
            orders = customerVisibility.ApplyMerchandiseOrderVisibility(orders, customerDb.Customers.AsNoTracking(), scope);
        }
        var query = DeliveryPlanSourceQuery.Project(orders, db.MfgOrderPOs.AsNoTracking(),
            db.DeliveryOrderDetails.AsNoTracking(), companyId, db.Employees.AsNoTracking());
        query = ForDate(query, date, request.IncludeOverdue, request.IncludeAllOrders);
        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
            query = query.Where(x => x.CustomerId == customerId);
        if (request.Progress.HasValue)
            query = request.Progress.Value switch
            {
                DeliveryPlanProgress.Delivered => query.Where(x => x.RemainingQuantity <= 0m),
                DeliveryPlanProgress.PartiallyDelivered => query.Where(x => x.RemainingQuantity > 0m && x.DeliveredQuantity > 0m),
                _ => query.Where(x => x.RemainingQuantity > 0m && x.DeliveredQuantity <= 0m)
            };
        if (request.NormalizedKeyword is { } keyword)
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(keyword);
            query = query.Where(x => EF.Functions.ILike(x.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.PONo, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.CustomerName, pattern, PostgresSearchPattern.EscapeCharacter));
        }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.ExternalId).ThenByDescending(x => x.Id)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize).Take(request.NormalizedPageSize).ToListAsync(ct);
        var linesByOrder = rows.Count == 0 ? new Dictionary<Guid, List<DeliveryPlanLineDto>>()
            : await readModel.LoadAsync(rows.Select(x => x.Id).ToArray(), companyId, ct);
        var now = clock.Now;
        var items = rows.Select(row => Map(row, linesByOrder.GetValueOrDefault(row.Id) ?? [], date, now)).ToArray();
        return OperationResult<DeliveryPlanResponseDto>.Ok(new DeliveryPlanResponseDto(date, request.IncludeOverdue,
            new PagedResult<DeliveryPlanItemDto>(items, count, request.NormalizedPageNumber, request.NormalizedPageSize), request.IncludeAllOrders));
    }

    internal static IQueryable<DeliveryPlanSource> ForDate(IQueryable<DeliveryPlanSource> query, DateOnly date, bool includeOverdue, bool includeAllOrders = false)
    {
        if (includeAllOrders) return query;
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        return query.Where(x => x.PromisedDate.HasValue &&
            (x.PromisedDate >= start && x.PromisedDate < end || includeOverdue && x.PromisedDate < start && x.RemainingQuantity > 0m));
    }

    internal static DateOnly Tomorrow(DateTime now)
        => Today(now).AddDays(1);

    private static DateOnly Today(DateTime now)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok")));

    internal static DeliveryPlanItemDto Map(DeliveryPlanSource row, IReadOnlyList<DeliveryPlanLineDto> lines, DateOnly date, DateTime now)
    {
        DateOnly? promised = row.PromisedDate is { } promisedDate ? DateOnly.FromDateTime(promisedDate) : null;
        var remaining = lines.Sum(x => x.RemainingQuantity);
        var delivered = lines.Sum(x => x.DeliveredQuantity);
        var orderStatus = new SaleOrderListItemDto
        {
            Status = row.Status, IsDeliveryPaused = row.IsDeliveryPaused,
            DeliveryPausedFrom = row.DeliveryPausedFrom, DeliveryPausedTo = row.DeliveryPausedTo
        };
        SaleOrderStatusRules.ApplyEffectivePausedStatus(orderStatus, now);
        return new DeliveryPlanItemDto
        {
            MerchandiseOrderId = row.Id, OrderExternalId = row.ExternalId, PONo = row.PONo, Status = orderStatus.Status, PromisedDeliveryDate = promised,
            CustomerId = row.CustomerId, CustomerName = row.CustomerName, Receiver = row.Receiver, Phone = row.Phone,
            SalesEmployeeFullName = row.SalesEmployeeFullName,
            DeliveryAddress = row.Address, Note = row.Note, IsDeliveryPaused = row.IsDeliveryPaused,
            DeliveryPauseReason = row.DeliveryPauseReason, DeliveryPausedFrom = row.DeliveryPausedFrom, DeliveryPausedTo = row.DeliveryPausedTo,
            OrderedQuantity = lines.Sum(x => x.OrderedQuantity), DeliveredQuantity = delivered, RemainingQuantity = remaining,
            AllocatedQuantity = lines.Sum(x => x.AllocatedQuantity), UnallocatedQuantity = lines.Sum(x => x.UnallocatedQuantity),
            IsOverdue = promised.HasValue && promised.Value < Today(now) && remaining > 0m, Progress = DeliveryPlanReadModel.Progress(delivered, remaining), Lines = lines
        };
    }
}
