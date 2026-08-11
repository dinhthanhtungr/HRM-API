using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Enums.Deliveries;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.ConfirmSaleOrderDelivery;

/// <summary>
/// Chot giao hang khi tat ca dong active da giao du. Sale manager cua don hoac nguoi co quyen duyet
/// duoc phep xac nhan; backend tu tinh luong da giao va khong nhan status tu FE.
/// </summary>
internal sealed class ConfirmSaleOrderDeliveryCommandHandler
    : IRequestHandler<ConfirmSaleOrderDeliveryCommand, OperationResult>
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventLogWriter _eventLogWriter;

    public ConfirmSaleOrderDeliveryCommandHandler(
        ISaleOrderDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IEventLogWriter eventLogWriter)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _eventLogWriter = eventLogWriter;
    }

    public async Task<OperationResult> Handle(
        ConfirmSaleOrderDeliveryCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MerchandiseOrderId == Guid.Empty)
        {
            return OperationResult.Fail("MerchandiseOrderId is invalid.");
        }

        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var order = await _dbContext.MerchandiseOrders
            .Include(x => x.MerchandiseOrderDetails)
            .FirstOrDefaultAsync(x =>
                x.MerchandiseOrderId == command.MerchandiseOrderId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);
        if (order is null)
        {
            return OperationResult.Fail("SaleOrder not found.");
        }

        if (order.ManagerById != employeeId && !SaleOrderApprovalRules.CanApprove(_currentUser))
        {
            return OperationResult.Fail("You do not have permission to confirm delivery for this SaleOrder.");
        }

        if (order.Status is nameof(MerchadiseStatus.Delivered) or nameof(MerchadiseStatus.Completed))
        {
            return OperationResult.Ok("SaleOrder delivery was already confirmed.");
        }

        if (order.Status is nameof(MerchadiseStatus.New) or nameof(MerchadiseStatus.Cancelled))
        {
            return OperationResult.Fail("Only an approved SaleOrder can be confirmed as delivered.");
        }

        var activeDetails = order.MerchandiseOrderDetails.Where(x => x.IsActive).ToList();
        if (activeDetails.Count == 0)
        {
            return OperationResult.Fail("SaleOrder has no active detail to confirm delivery.");
        }

        var detailIds = activeDetails.Select(x => x.MerchandiseOrderDetailId).ToArray();
        var deliveredQuantities = await _dbContext.DeliveryOrderDetails
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                !x.IsAttach &&
                x.MerchandiseOrderDetailId.HasValue &&
                detailIds.Contains(x.MerchandiseOrderDetailId.Value) &&
                x.DeliveryOrder.IsActive &&
                x.DeliveryOrder.CompanyId == companyId &&
                x.DeliveryOrder.Status != DeliveryOrderStatus.Canceled.ToString() &&
                x.DeliveryOrder.Status != "Cancelled")
            .GroupBy(x => x.MerchandiseOrderDetailId!.Value)
            .Select(x => new { DetailId = x.Key, Quantity = x.Sum(item => item.Quantity) })
            .ToDictionaryAsync(x => x.DetailId, x => x.Quantity, cancellationToken);

        var incompleteDetail = activeDetails.FirstOrDefault(x =>
            deliveredQuantities.GetValueOrDefault(x.MerchandiseOrderDetailId) < x.ExpectedQuantity);
        if (incompleteDetail is not null)
        {
            return OperationResult.Fail(
                $"Detail {incompleteDetail.MerchandiseOrderDetailId} has not been delivered in full.");
        }

        var now = _dateTimeProvider.Now;
        foreach (var detail in activeDetails)
        {
            detail.Status = MerchadiseStatus.Delivered.ToString();
        }

        order.Status = MerchadiseStatus.Delivered.ToString();
        order.UpdatedBy = employeeId;
        order.UpdatedDate = now;

        await _eventLogWriter.AddAsync(new EventLogCreateRequest
        {
            EmployeeId = employeeId,
            CompanyId = order.CompanyId,
            SourceType = "MerchandiseOrder",
            SourceId = order.MerchandiseOrderId,
            SourceCode = order.ExternalId,
            EventType = EventType.MerchadiseStatus,
            Status = order.Status,
            Note = $"Confirmed delivery for Merchandise Order {order.ExternalId}",
            CreatedDate = now
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok($"SaleOrder {order.ExternalId} was confirmed as delivered.");
    }
}
