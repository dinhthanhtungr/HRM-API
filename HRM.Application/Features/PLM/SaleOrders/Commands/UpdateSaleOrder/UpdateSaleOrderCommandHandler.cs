using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.UpdateSaleOrder;

/// <summary>
/// Tìm SaleOrder active theo CompanyId, áp dụng patch có kiểm soát và cập nhật audit.
/// Trạng thái không thuộc contract này; mọi chuyển trạng thái phải đi qua command nghiệp vụ riêng.
/// </summary>
internal sealed class UpdateSaleOrderCommandHandler : IRequestHandler<UpdateSaleOrderCommand, OperationResult>
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateSaleOrderCommandHandler(
        ISaleOrderDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult> Handle(UpdateSaleOrderCommand command, CancellationToken cancellationToken)
    {
        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        var request = command.Request;
        if (request.MerchandiseOrderId == Guid.Empty)
        {
            return OperationResult.Fail("MerchandiseOrderId không hợp lệ.");
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var order = await _dbContext.MerchandiseOrders
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.MerchandiseOrderId == request.MerchandiseOrderId && x.IsActive, cancellationToken);
        if (order is null)
        {
            return OperationResult.Fail("Không tìm thấy đơn hàng.");
        }

        PatchHelper.SetTrimmed(request.CustomerNameSnapshot, () => order.CustomerNameSnapshot, value => order.CustomerNameSnapshot = value ?? string.Empty);
        PatchHelper.SetTrimmed(request.CustomerExternalIdSnapshot, () => order.CustomerExternalIdSnapshot, value => order.CustomerExternalIdSnapshot = value ?? string.Empty);
        PatchHelper.SetTrimmed(request.PhoneSnapshot, () => order.PhoneSnapshot, value => order.PhoneSnapshot = value ?? string.Empty);
        PatchHelper.SetTrimmed(request.Receiver, () => order.Receiver, value => order.Receiver = value ?? string.Empty);
        PatchHelper.SetTrimmed(request.DeliveryAddress, () => order.DeliveryAddress, value => order.DeliveryAddress = value ?? string.Empty);
        PatchHelper.SetNullable(request.Vat, () => order.Vat, value => order.Vat = value);
        PatchHelper.SetTrimmed(request.PaymentType, () => order.PaymentType, value => order.PaymentType = value);
        PatchHelper.SetNullable(request.PaymentDate, () => order.PaymentDate, value => order.PaymentDate = value);
        PatchHelper.SetTrimmed(request.Note, () => order.Note, value => order.Note = value);
        PatchHelper.SetTrimmed(request.ShippingMethod, () => order.ShippingMethod, value => order.ShippingMethod = value);
        PatchHelper.SetTrimmed(request.PONo, () => order.PONo, value => order.PONo = value ?? string.Empty);

        order.UpdatedBy = employeeId;
        order.UpdatedDate = _dateTimeProvider.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok("Cập nhật thành công.");
    }
}
