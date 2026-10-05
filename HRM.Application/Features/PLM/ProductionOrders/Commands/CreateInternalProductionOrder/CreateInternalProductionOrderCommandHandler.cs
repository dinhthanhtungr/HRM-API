using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.ProductionOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ProductionOrders.Commands.CreateInternalProductionOrder;

internal sealed class CreateInternalProductionOrderCommandHandler(
    IProductionOrderDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions,
    IExternalIdService externalIds, IDateTimeProvider clock, IEventLogWriter timeline)
    : IRequestHandler<CreateInternalProductionOrderCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(CreateInternalProductionOrderCommand command, CancellationToken ct)
    {
        var error = ProductionOrderCreationAccess.Validate(user, permissions) ??
                    ProductionOrderCreationRules.ValidateInternal(command.Request);
        if (error is not null) return OperationResult<Guid>.Fail(error);
        var request = command.Request;
        var companyId = user.CompanyId!.Value;
        var employeeId = user.EmployeeId!.Value;

        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            var product = await db.Products.AsNoTracking()
                .Where(x => x.ProductId == request.ProductId && x.CompanyId == companyId && x.IsActive)
                .Select(x => new { x.ProductId, x.ColourCode, x.Name, x.ColourName }).FirstOrDefaultAsync(ct);
            if (product is null) return OperationResult<Guid>.Fail("Không tìm thấy sản phẩm trong công ty.");
            var customer = request.CustomerId.HasValue
                ? await db.Customers.AsNoTracking().Where(x => x.CustomerId == request.CustomerId &&
                    x.CompanyId == companyId && x.IsActive == true)
                    .Select(x => new { x.CustomerId, x.ExternalId, x.CustomerName }).FirstOrDefaultAsync(ct) : null;
            if (request.CustomerId.HasValue && customer is null)
                return OperationResult<Guid>.Fail("Không tìm thấy khách hàng trong công ty.");
            var formula = request.FormulaId.HasValue
                ? await db.Formulas.AsNoTracking().Where(x => x.FormulaId == request.FormulaId &&
                    x.CompanyId == companyId && x.IsActive && x.ProductId == product.ProductId)
                    .Select(x => new { x.FormulaId, x.ExternalId }).FirstOrDefaultAsync(ct) : null;
            if (request.FormulaId.HasValue && formula is null)
                return OperationResult<Guid>.Fail("Không tìm thấy công thức của sản phẩm trong công ty.");

            var now = clock.Now;
            var order = new MfgProductionOrder
            {
                MfgProductionOrderId = Guid.NewGuid(),
                ExternalId = await externalIds.GenerateMonthlyCodeAsync(companyId, DocumentPrefix.MFG.ToString(), ct),
                ProductId = product.ProductId, ProductExternalIdSnapshot = product.ColourCode,
                ProductNameSnapshot = product.Name, ColorName = product.ColourName,
                CustomerId = customer?.CustomerId, CustomerExternalIdSnapshot = customer?.ExternalId,
                CustomerNameSnapshot = customer?.CustomerName, FormulaId = formula?.FormulaId,
                FormulaExternalIdSnapshot = formula?.ExternalId,
                ManufacturingDate = request.ManufacturingDate, ExpectedDate = request.ExpectedDate,
                RequiredDate = request.RequiredDate, TotalQuantityRequest = request.TotalQuantityRequest,
                TotalQuantity = request.TotalQuantity, NumOfBatches = request.NumOfBatches, UnitPriceAgreed = request.UnitPriceAgreed,
                Status = string.IsNullOrWhiteSpace(request.InitialStatus)
                    ? ManufacturingProductOrder.New.ToString() : request.InitialStatus.Trim(),
                LabNote = request.LabNote, Requirement = request.Requirement, PlpuNote = request.PlpuNote,
                BagType = request.BagType, QcCheck = request.QcCheck, StepOfProduct = request.StepOfProduct,
                IsActive = true, CompanyId = companyId, CreatedDate = now, UpdatedDate = now,
                CreatedBy = employeeId, UpdatedBy = employeeId
            };
            await db.MfgProductionOrders.AddAsync(order, ct);
            await timeline.AddAsync(new EventLogCreateRequest
            {
                EmployeeId = employeeId, CompanyId = companyId, SourceType = "MfgProductionOrder",
                SourceId = order.MfgProductionOrderId, SourceCode = order.ExternalId,
                EventType = EventType.ManufacturingProductOrder, Status = order.Status,
                Note = $"Tạo lệnh sản xuất nội bộ vào {now} bởi {user.UserName}", CreatedDate = now
            }, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<Guid>.Ok(order.MfgProductionOrderId, "Tạo lệnh sản xuất nội bộ thành công.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return OperationResult<Guid>.Fail("Có lỗi xảy ra trong quá trình tạo lệnh sản xuất nội bộ.");
        }
    }
}
