using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.ApproveSaleOrder;

/// <summary>
/// Kiểm tra quyền truy cập theo CompanyId, chặn duyệt lại dòng đã có MFG, tạo MFG và ghi EventLog
/// trong cùng transaction trước khi chuyển trạng thái SaleOrder sang Approved.
/// </summary>
internal sealed class ApproveSaleOrderCommandHandler : IRequestHandler<ApproveSaleOrderCommand, OperationResult>
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SaleOrderApprovalService _approvalService;

    public ApproveSaleOrderCommandHandler(
        ISaleOrderDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        SaleOrderApprovalService approvalService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _approvalService = approvalService;
    }

    public async Task<OperationResult> Handle(ApproveSaleOrderCommand command, CancellationToken cancellationToken)
    {
        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        if (!SaleOrderApprovalRules.CanApprove(_currentUser))
        {
            return OperationResult.Fail("Bạn không có quyền duyệt đơn hàng.");
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var order = await _dbContext.MerchandiseOrders
            .Include(x => x.MerchandiseOrderDetails)
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.MerchandiseOrderId == command.MerchandiseOrderId && x.IsActive, cancellationToken);
        if (order is null)
        {
            return OperationResult.Fail("Không tìm thấy đơn hàng.");
        }

        var now = _dateTimeProvider.Now;
        var approvalResult = await _approvalService.ApproveAsync(
            order,
            employeeId,
            now,
            cancellationToken);
        if (!approvalResult.Success)
        {
            return approvalResult;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return approvalResult;
    }
}
