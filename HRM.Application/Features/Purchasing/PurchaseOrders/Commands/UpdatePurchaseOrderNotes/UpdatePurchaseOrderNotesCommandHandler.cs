using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.DeletePurchaseOrder;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.UpdatePurchaseOrderNotes;

internal sealed class UpdatePurchaseOrderNotesCommandHandler(
    ISender sender,
    IPurchaseOrderDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdatePurchaseOrderNotesCommand, OperationResult>
{
    public async Task<OperationResult> Handle(UpdatePurchaseOrderNotesCommand command, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !PurchaseOrderAccess.CanManage(permissions))
            return OperationResult.Fail("Bạn không có quyền cập nhật đơn mua hàng.");
        if (!currentUser.CompanyId.HasValue || !currentUser.EmployeeId.HasValue || currentUser.CompanyId == Guid.Empty || currentUser.EmployeeId == Guid.Empty)
            return OperationResult.Fail("Không xác định được công ty hoặc nhân viên hiện tại.");

        var original = command.Request;
        command = command with { Request = PurchaseOrderNoteRules.IgnoreClientDeliveryDate(original) };
        var ignoredDateOnly = (original.RealDeliveryDate.HasValue || original.ClearFields?.Contains("realDeliveryDate", StringComparer.OrdinalIgnoreCase) == true) &&
            command.Request.ClearFields is { Count: 0 } && command.Request.OrderType is null && command.Request.RequestDeliveryDate is null &&
            command.Request.PaymentTypes is null && command.Request.Vat is null && command.Request.DeliveryAddress is null &&
            command.Request.Comment is null && command.Request.PlpuComment is null && command.Request.IsActive is null;
        if (!ignoredDateOnly && PurchaseOrderNoteRules.Validate(command.Request) is { } error)
            return OperationResult.Fail(error);
        if (command.Request.IsActive == false)
            return await sender.Send(new DeletePurchaseOrderCommand(command.PurchaseOrderId, command.Request), cancellationToken);

        var companyId = currentUser.CompanyId.Value;
        var employeeId = currentUser.EmployeeId.Value;
        if (!await dbContext.Employees.AnyAsync(x => x.EmployeeId == employeeId && x.CompanyId == companyId && x.IsActive, cancellationToken))
            return OperationResult.Fail("Nhân viên không thuộc công ty hiện tại.");
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await dbContext.LockPurchaseOrderAsync(command.PurchaseOrderId, companyId, cancellationToken);
        var purchaseOrder = await dbContext.PurchaseOrders
            .Include(x => x.PurchaseOrderSnapshot).Include(x => x.PurchaseOrderDetails)
            .Include(x => x.Supplier).ThenInclude(x => x!.SupplierAddresses)
            .Include(x => x.Supplier).ThenInclude(x => x!.SupplierContacts)
            .Include(x => x.CreatedByNavigation)
            .FirstOrDefaultAsync(item =>
            item.PurchaseOrderId == command.PurchaseOrderId &&
            item.CompanyId == currentUser.CompanyId.Value &&
            item.IsActive == true,
            cancellationToken);
        if (purchaseOrder is null)
            return OperationResult.Fail("Không tìm thấy đơn mua hàng.");
        if (ignoredDateOnly) return OperationResult.Ok("Ngày giao thực tế được xác định từ IQC; không cập nhật từ client.");
        if (!PurchaseOrderEditRules.CanEdit(purchaseOrder) &&
            !(PurchaseOrderEditRules.CanEditInternalNote(purchaseOrder) && PurchaseOrderNoteRules.IsInternalNoteOnly(command.Request)))
            return OperationResult.Fail(PurchaseOrderEditRules.LockedMessage + " Đang giao/hoàn thành chỉ được sửa ghi chú nội bộ.");
        var subtotal = purchaseOrder.PurchaseOrderDetails.Where(x => x.IsActive)
            .Sum(x => x.TotalPriceAgreed ?? 0m);
        var supplier = purchaseOrder.Supplier?.CompanyId == companyId ? purchaseOrder.Supplier : null;
        var creator = purchaseOrder.CreatedByNavigation?.CompanyId == companyId ? purchaseOrder.CreatedByNavigation : null;
        var snapshot = purchaseOrder.PurchaseOrderSnapshot ?? new HRM.Domain.Entities.OrderSchema.PurchaseOrderSnapshot
        {
            PurchaseOrderSnapshotId = Guid.CreateVersion7(), TotalPrice = subtotal,
            SupplierNameSnapshot = supplier?.SupplierName, SupplierExternalIdSnapshot = supplier?.ExternalId,
            SupplierPhoneNumberSnapshot = supplier?.Phone,
            SupplierAddressSnapshot = supplier?.SupplierAddresses.Where(x => x.IsActive == true && x.IsPrimary == true).Select(x => x.AddressLine).FirstOrDefault(),
            SupplierContactSnapshot = supplier?.SupplierContacts.Where(x => x.IsActive == true)
                .OrderByDescending(x => x.IsPrimary == true).ThenBy(x => x.ContactId)
                .Select(x => ((x.FirstName ?? string.Empty) + " " + (x.LastName ?? string.Empty)).Trim()).FirstOrDefault(),
            EmployeeExternalIdSnapshot = creator?.ExternalId ?? string.Empty,
            EmployeeFullNameSnapshot = creator?.FullName ?? string.Empty, PhoneNumberSnapshot = creator?.PhoneNumber, EmailSnapshot = creator?.Email
        };
        if (!PurchaseOrderNoteRules.ApplyInformation(purchaseOrder, snapshot, command.Request))
            return OperationResult.Fail("Không có nội dung thay đổi.");
        var request = command.Request;
        var changesSnapshot = request.PaymentTypes is not null || request.DeliveryAddress is not null || request.Vat.HasValue ||
            request.ClearFields.Any(x => new[] { "paymentTypes", "deliveryAddress", "vat" }.Contains(x, StringComparer.OrdinalIgnoreCase));
        if (request.Vat.HasValue || request.ClearFields.Contains("vat", StringComparer.OrdinalIgnoreCase))
            snapshot.TotalPrice = PurchaseOrderTotals.Calculate(subtotal, snapshot.Vat);
        if (changesSnapshot && purchaseOrder.PurchaseOrderSnapshot is null)
        {
            await dbContext.PurchaseOrderSnapshots.AddAsync(snapshot, cancellationToken);
            purchaseOrder.PurchaseOrderSnapshotId = snapshot.PurchaseOrderSnapshotId;
            purchaseOrder.PurchaseOrderSnapshot = snapshot;
        }

        purchaseOrder.UpdatedBy = currentUser.EmployeeId.Value;
        purchaseOrder.UpdatedDate = dateTimeProvider.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok("Đã cập nhật thông tin đơn mua hàng.");
    }
}
