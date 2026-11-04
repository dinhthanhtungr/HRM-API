using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateProductionOrderInform;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CreateBasicInformProductionOrder;

internal sealed class CreateBasicInformProductionOrderCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    ManufacturingFormulaInputResolver resolver,
    ISender sender
) : IRequestHandler<CreateBasicInformProductionOrderCommand, OperationResult<CreateProductionOrderInformResult>>
{
    public async Task<OperationResult<CreateProductionOrderInformResult>> Handle(CreateBasicInformProductionOrderCommand command, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.CreateProductionOrders, true);
        if (error is not null)
        {
            return Fail(error);
        }

        var input = command.Request;
        if (input.StepOfProduct is null || !Enum.IsDefined(input.StepOfProduct.Value))
        {
            return Fail("Phải chọn công đoạn sản xuất hợp lệ.");
        }

        if (input.MerchandiseOrderId == Guid.Empty || input.MerchandiseOrderDetailId == Guid.Empty
            || input.FormulaId == Guid.Empty
            || input.ManufacturingFormulaId == Guid.Empty
            || input.RequiredDate == default
            || input.Items?.Count > 1000)
        {
            return Fail("Reference hoặc dữ liệu lệnh sản xuất không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var detail = await db.MerchandiseOrderDetails.AsNoTracking().Where(x => x.MerchandiseOrderDetailId == input.MerchandiseOrderDetailId
            && x.MerchandiseOrderId == input.MerchandiseOrderId
            && x.IsActive
            && x.MerchandiseOrder.CompanyId == company
            && x.MerchandiseOrder.IsActive)
            .Include(x => x.MerchandiseOrder)
            .FirstOrDefaultAsync(ct);
        if (detail is null)
        {
            return Fail("Không tìm thấy chi tiết đơn bán trong công ty.");
        }

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.ProductId == detail.ProductId && x.CompanyId == company && x.IsActive, ct);
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == detail.MerchandiseOrder.CustomerId && x.CompanyId == company && x.IsActive == true, ct);
        if (product is null || customer is null)
        {
            return Fail("Sản phẩm hoặc khách hàng không hợp lệ.");
        }

        var request = new CreateProductionOrderInformRequest

        {

            MerchandiseOrderId = detail.MerchandiseOrderId,

            MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId,

            MerchandiseOrderExternalId = detail.MerchandiseOrder.ExternalId,

            ProductId = detail.ProductId,

            ProductExternalIdSnapshot = detail.ProductExternalIdSnapshot,

            ProductNameSnapshot = detail.ProductNameSnapshot,

            CustomerId = detail.MerchandiseOrder.CustomerId,

            CustomerNameSnapshot = detail.MerchandiseOrder.CustomerNameSnapshot,

            CustomerExternalIdSnapshot = detail.MerchandiseOrder.CustomerExternalIdSnapshot,

            UnitPriceAgreed = detail.UnitPriceAgreed,

            RequiredDate = input.RequiredDate,

            ExpectedDate = input.ExpectedDate,

            ManufacturingDate = input.ManufacturingDate,

            TotalQuantityRequest = input.TotalQuantityRequest,

            TotalQuantity = input.TotalQuantity,

            NumOfBatches = input.NumOfBatches,

            StepOfProduct = input.StepOfProduct,

            BagType = input.BagType,

            LabNote = input.LabNote,

            Requirement = input.Requirement,

            PlpuNote = ManufacturingNoteRules.PreserveStoredPlpuAuto(input.PlpuNote, null),

            QcCheck = input.QcCheck

        };
        var sourceId = input.ManufacturingFormulaId ?? input.FormulaId ?? Guid.Empty;
        var origin = input.ManufacturingFormulaId.HasValue ? ManufacturingFormulaOrigin.Production : ManufacturingFormulaOrigin.FromVu;
        if (input.FormulaId.HasValue)
        {
            var formula = await db.Formulas.AsNoTracking().FirstOrDefaultAsync(x => x.FormulaId == input.FormulaId && x.ProductId == detail.ProductId && x.CompanyId == company
                && x.IsActive, ct);
            if (formula is null)
            {
                return Fail("VU không thuộc sản phẩm trong công ty.");
            }

            request.FormulaCustomerSelect = formula.FormulaId;
            request.FormulaCustomerExternalIdSelect = formula.ExternalId;
        }

        if (input.ManufacturingFormulaId.HasValue)
        {
            var formula = await db.ManufacturingFormulas.AsNoTracking().FirstOrDefaultAsync(x => x.ManufacturingFormulaId == sourceId && x.CompanyId == company && x.IsActive, ct);
            var belongs = await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.ManufacturingFormulaId == sourceId && x.CompanyId == company
                && x.MfgProductionOrder.CompanyId == company
                && x.MfgProductionOrder.IsActive
                && x.MfgProductionOrder.ProductId == detail.ProductId, ct);
            if (formula is null || !belongs)
            {
                return Fail("VA không thuộc sản phẩm trong công ty.");
            }

            request.ManufacturingFormulaIdIsSelect = sourceId;
            request.ManufacturingFormulaExternalIdIsSelect = formula.ExternalId;
        }

        // [] tạo MFG không VA; null dùng thành phần nguồn nếu có nguồn được chọn.
        if (input.Items is { Count: > 0 } || input.Items is null && sourceId != Guid.Empty)
        {
            var order = new MfgProductionOrder

            {

                CompanyId = company,

                ProductId = detail.ProductId

            };
            var source = new ProductionOrderFormulaOptionDto(sourceId, string.Empty, origin, null, default, false);
            var resolved = await resolver.ResolveAsync(order, source, input.Items, ct);
            if (!resolved.Success || resolved.Data is null)
            {
                return Fail(resolved.Message ?? "Thành phần công thức không hợp lệ.");
            }

            request.FormulaItems = resolved.Data;
            request.PlpuNote = await resolver.BuildPlpuNoteAsync(order, source, input.Items, resolved.Data, input.PlpuNote, ct);
        }

        // Writer tương thích tiếp tục sở hữu transaction, validate references, lịch, giữ hàng và audit.
        return await sender.Send(new CreateProductionOrderInformCommand(request), ct);
    }

    private static OperationResult<CreateProductionOrderInformResult> Fail(string message) => OperationResult<CreateProductionOrderInformResult>.Fail(message);
}
