using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.Pricing.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrder;

internal sealed class GetProductionOrderQueryHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IPricingVisibilityService pricing
) : IRequestHandler<GetProductionOrderQuery, OperationResult<GetMfgProductionOrder>>
{
    public async Task<OperationResult<GetMfgProductionOrder>> Handle(GetProductionOrderQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null)
        {
            return OperationResult<GetMfgProductionOrder>.Fail(error);
        }

        if (request.Id == Guid.Empty)
        {
            return OperationResult<GetMfgProductionOrder>.Fail("Id không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var selections = db.ProductionSelectVersions.AsNoTracking().Where(s => s.CompanyId == company && s.ValidTo == null
            && (s.ManufacturingFormulaId == null
            || s.ManufacturingFormula != null && s.ManufacturingFormula.CompanyId == company));
        var formulas = db.ManufacturingFormulas.AsNoTracking().Where(f => f.CompanyId == company && f.IsActive);
        var links = db.MfgOrderPOs.AsNoTracking().Where(l => l.IsActive && l.Detail.IsActive && l.Detail.MerchandiseOrder.CompanyId == company
            && l.ProductionOrder.CompanyId == company);
        var canViewPrice = pricing.GetAccess().CanViewApprovedSellingPrice;
        var dto = await db.MfgProductionOrders.AsNoTracking().Where(o => o.MfgProductionOrderId == request.Id && o.CompanyId == company && o.IsActive)
            .Select(o => new GetMfgProductionOrder
        {
            MfgProductionOrderId = o.MfgProductionOrderId,
            PoNo = links.Where(l => l.MfgProductionOrderId == o.MfgProductionOrderId)
                .OrderByDescending(l => l.Detail.MerchandiseOrderId)
                .Select(l => l.Detail.MerchandiseOrder.IsActive ? l.Detail.MerchandiseOrder.PONo : null)
                .FirstOrDefault(),
            ExternalId = o.ExternalId,
            MerchandiseOrderId = links.Where(l => l.MfgProductionOrderId == o.MfgProductionOrderId).OrderByDescending(l => l.Detail.MerchandiseOrderId)
                .Select(l => l.Detail.MerchandiseOrderId)
                .FirstOrDefault(),
            MerchandiseOrderExternalId = links.Where(l => l.MfgProductionOrderId == o.MfgProductionOrderId).OrderByDescending(l => l.Detail.MerchandiseOrderId)
                .Select(l => l.Detail.MerchandiseOrder.ExternalId)
                .FirstOrDefault(),
            ManufacturingFormulaIdIsSelect = selections.Where(s => s.MfgProductionOrderId == o.MfgProductionOrderId).OrderByDescending(s => s.ValidFrom)
                .ThenByDescending(s => s.ProductionSelectVersionId)
                .Select(s => s.ManufacturingFormulaId)
                .FirstOrDefault(),
            ManufacturingFormulaExternalIdIsSelect = (
            from s in selections
            join f in formulas on s.ManufacturingFormulaId equals f.ManufacturingFormulaId
            where s.MfgProductionOrderId == o.MfgProductionOrderId
            orderby s.ValidFrom descending, s.ProductionSelectVersionId descending
            select f.ExternalId).FirstOrDefault(),
            CustomerNameSnapshot = o.CustomerNameSnapshot,
            CustomerExternalIdSnapshot = o.CustomerExternalIdSnapshot,
            ProductId = o.ProductId,
            ProductExternalIdSnapshot = o.ProductExternalIdSnapshot,
            ProductNameSnapshot = o.ProductNameSnapshot,
            SamplerRequestExternalIdSnapshot = o.Product.SampleRequests.Where(sr => sr.IsActive && sr.CompanyId == company).OrderByDescending(sr => sr.CreatedDate)
                .Select(sr => sr.ExternalId)
                .FirstOrDefault(),
            FormulaCustomerSelect = o.FormulaId ?? Guid.Empty,
            FormulaCustomerExternalIdSelect = o.FormulaExternalIdSnapshot ?? "",
            ManufacturingDate = o.ManufacturingDate,
            ExpectedDate = o.ExpectedDate,
            RequiredDate = o.RequiredDate,
            TotalQuantityRequest = o.TotalQuantityRequest,
            TotalQuantity = o.TotalQuantity,
            NumOfBatches = o.NumOfBatches,
            UnitPriceAgreed = canViewPrice ? (decimal? )o.UnitPriceAgreed : null,
            StepOfProduct = o.StepOfProduct,
            Status = o.Status,
            LabNote = o.LabNote,
            Requirement = o.Requirement,
            PlpuNote = o.PlpuNote,
            BagType = o.BagType,
            QcCheck = o.QcCheck
        })
            .FirstOrDefaultAsync(ct);
        if (dto is null)
        {
            return OperationResult<GetMfgProductionOrder>.Fail("Không tìm thấy lệnh sản xuất trong công ty.");
        }

        // Chỉ đọc lịch đã lưu của các dòng thuộc đúng đơn bán đang hiển thị trên MFG.
        dto.RequestedDeliverySchedules = await links
            .Where(l => l.MfgProductionOrderId == request.Id
                && l.Detail.MerchandiseOrderId == dto.MerchandiseOrderId
                && l.Detail.MerchandiseOrder.IsActive)
            .SelectMany(l => l.Detail.DeliverySchedules.Where(s => s.IsActive))
            .Select(s => new { s.MerchandiseOrderDeliveryScheduleId, s.RequestedDate, s.Quantity, s.MerchandiseOrderDetailId, s.PromisedDate })
            .Distinct()
            .OrderBy(s => s.RequestedDate).ThenBy(s => s.MerchandiseOrderDeliveryScheduleId)
            .Select(s => new ProductionRequestedDeliveryDto(s.MerchandiseOrderDeliveryScheduleId, s.RequestedDate, s.Quantity,
                s.MerchandiseOrderDetailId, s.PromisedDate))
            .ToArrayAsync(ct);

        if (await selections.CountAsync(s => s.MfgProductionOrderId == request.Id, ct) > 1)
        {
            return OperationResult<GetMfgProductionOrder>.Fail("Lệnh có nhiều hơn một VA đang chọn. Cần kiểm tra dữ liệu.");
        }

        return OperationResult<GetMfgProductionOrder>.Ok(dto);
    }
}
