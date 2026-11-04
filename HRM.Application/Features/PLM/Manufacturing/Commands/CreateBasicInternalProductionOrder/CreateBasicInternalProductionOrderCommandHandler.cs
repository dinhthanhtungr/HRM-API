using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateInternalProductionOrder;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CreateBasicInternalProductionOrder;

internal sealed class CreateBasicInternalProductionOrderCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IMaterialPriceQueryService prices,
    IDateTimeProvider clock,
    ISender sender
) : IRequestHandler<CreateBasicInternalProductionOrderCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(CreateBasicInternalProductionOrderCommand command, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.CreateProductionOrders, true);
        if (error is not null)
        {
            return OperationResult<Guid>.Fail(error);
        }

        var r = command.Request;
        var request = new CreateInternalProductionOrderRequest

        {

            ProductId = r.ProductId,

            FormulaId = r.FormulaId,

            CustomerId = r.CustomerId,

            RequiredDate = r.RequiredDate ?? clock.Now.Date,

            TotalQuantityRequest = r.TotalQuantityRequest,

            TotalQuantity = r.TotalQuantity,

            NumOfBatches = r.NumOfBatches,

            StepOfProduct = r.StepOfProduct,

            ExpectedDate = r.ExpectedDate,

            ManufacturingDate = r.ManufacturingDate,

            BagType = r.BagType,

            LabNote = r.LabNote,

            PlpuNote = r.PlpuNote,

            Requirement = r.Requirement,

            QcCheck = r.QcCheck

        };
        var validation = ProductionOrderCreationRules.ValidateInternal(request);
        if (validation is not null || r.FormulaId == Guid.Empty || r.CustomerId == Guid.Empty
            || r.RequiredDate == DateTime.MinValue)
        {
            return OperationResult<Guid>.Fail(validation ?? "Reference hoặc ngày yêu cầu không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        if (r.FormulaId.HasValue)
        {
            var formula = await db.Formulas.AsNoTracking().Where(x => x.FormulaId == r.FormulaId && x.ProductId == r.ProductId && x.CompanyId == company && x.IsActive)
                .Select(x => new { x.FormulaId })
                .FirstOrDefaultAsync(ct);
            if (formula is null)
            {
                return OperationResult<Guid>.Fail("Không tìm thấy VU của sản phẩm trong công ty.");
            }

            var rows = await db.FormulaMaterials.AsNoTracking().Where(x => x.FormulaId == formula.FormulaId && x.Formula.CompanyId == company && x.IsActive)
                .Select(x => new { x.itemType, x.MaterialId, x.Quantity, x.UnitPrice })
                .ToListAsync(ct);
            var materialIds = rows.Where(x => ProductionOrderCreationRules.IsMaterial(x.itemType)).Select(x => x.MaterialId)
                .Distinct()
                .ToArray();
            // The old form suggested latest NVL costs and kept stored Product costs.
            var latest = await prices.LoadLatestPricingItemPriceInfoDictAsync(
                company,
                "VND",
                materialIds.Where(x => x.HasValue).Select(x => new PriceItemRequest
                {
                    ItemType = ItemType.Material,
                    MaterialId = x
                }),
                ct
            );
            var total = rows.Sum(x => x.Quantity * (ProductionOrderCreationRules.IsMaterial(x.itemType) && x.MaterialId.HasValue
                && latest.TryGetValue(new(ItemType.Material, x.MaterialId.Value), out var price) ? price.CurrentPrice : x.UnitPrice));
            request.UnitPriceAgreed = total > 0 ? total : 0;
        }

        // The existing write handler owns validation, transaction, audit and MFG code generation.
        // No client status/snapshots/cost can enter this new contract.
        return await sender.Send(new CreateInternalProductionOrderCommand(request), ct);
    }
}
