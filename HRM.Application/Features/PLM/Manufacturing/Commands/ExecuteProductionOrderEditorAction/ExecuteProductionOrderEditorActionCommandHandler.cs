using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;
using HRM.Application.Features.PLM.Manufacturing.Commands.CreateProductionOrderFormula;
using HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInform;
using HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInformation;
using HRM.Application.Features.PLM.Manufacturing.Commands.UseProductionOrderStock;
using HRM.Application.Features.PLM.Manufacturing.Commands.RollbackProductionOrderStock;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.PLM.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction;

/// <summary>Adapter editor thống nhất; writer hiện hữu sở hữu transaction và kiểm tra revision sau khóa.</summary>
internal sealed class ExecuteProductionOrderEditorActionCommandHandler(
    IManufacturingDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions,
    IPLMFieldVisibilityService visibility, ISender sender)
    : IRequestHandler<ExecuteProductionOrderEditorActionCommand, OperationResult>
{
    public async Task<OperationResult> Handle(ExecuteProductionOrderEditorActionCommand command, CancellationToken ct)
    {
        var error = ManufacturingAccess.ValidateEditor(user, permissions);
        if (error is not null) return OperationResult.Fail(error);
        var r = command.Request;
        if ((r.FormulaNote is not null || r.ClearFields?.Contains("formulaNote") == true)
            && (!permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingFormulas)
                || !visibility.CanViewFormulaMaterials()))
            return OperationResult.Fail("Bạn không có quyền sửa ghi chú VA.");
        var canManage = ManufacturingAccess.CanManageInform(user, permissions);
        if (!canManage && (r.LabNote is not null || r.Requirement is not null))
            return OperationResult.Fail("LabUser chỉ được sửa QC, ghi chú công thức và NVL trong Inform.");
        if (!canManage && (r.PlpuNote is not null || r.ClearFields?.Contains("plpuNote") == true))
            return OperationResult.Fail("Bạn không có quyền sửa Ghi chú PLPU.");
        if ((r.Requirement is not null || r.ClearFields?.Contains("requirement") == true)
            && !permissions.HasPermission(ApplicationPermissions.PLM.EditProductionOrderRequirement))
            return OperationResult.Fail("Bạn không có quyền sửa nội dung Yêu cầu.");
        if (!canManage && (r.TotalQuantity.HasValue || r.NumOfBatches.HasValue || r.StepOfProduct.HasValue
            || r.ExpectedDate.HasValue || r.ManufacturingDate.HasValue || r.BagType is not null
            || r.ClearFields is not null && r.ClearFields.Any(x => !ManufacturingEditorPolicy.FormulaNoteFields.Contains(x))))
            return OperationResult.Fail("Bạn chỉ được sửa công thức và ghi chú, không được sửa thông tin lệnh.");
        if (command.Id == Guid.Empty || string.IsNullOrWhiteSpace(r.Revision) || r.Revision.Length != 64)
            return OperationResult.Fail("Thiếu revision của lệnh. Vui lòng tải lại Inform.");
        error = ProductionOrderEditorMapping.Validate(r);
        if (error is not null) return OperationResult.Fail(error);
        if (r.Items is not null && !visibility.CanViewFormulaMaterials())
            return OperationResult.Fail("Bạn không có quyền sửa thành phần công thức.");
        var order = await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MfgProductionOrderId == command.Id
            && x.CompanyId == user.CompanyId && x.IsActive, ct);
        if (order is null) return OperationResult.Fail("Không tìm thấy lệnh trong công ty.");
        var hasLink = await db.MfgOrderPOs.AsNoTracking().AnyAsync(x => x.MfgProductionOrderId == command.Id && x.IsActive
            && x.Detail.IsActive && x.Detail.MerchandiseOrder.IsActive && x.Detail.MerchandiseOrder.CompanyId == user.CompanyId, ct);
        if (!ManufacturingEditorPolicy.Actions(order.Status, hasLink && canManage, visibility.CanViewFormulaMaterials(), true,
            hasLink && permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingFormulas)).Contains(r.Action))
            return OperationResult.Fail("Thao tác không phù hợp quyền, trạng thái hoặc liên kết của lệnh.");
        if (!await ManufacturingEditorRevision.MatchesAsync(db, order, r.Revision, ct))
            return OperationResult.Fail(ManufacturingEditorRevision.Conflict);
        if (r.Action == "RollbackUseStock")
            return await sender.Send(new RollbackProductionOrderStockCommand(command.Id, r.Revision), ct);
        var merged = ProductionOrderEditorMapping.Merge(order, r);
        if (r.Action == "Save")
            return await sender.Send(new UpdateProductionOrderInformCommand(command.Id, merged)
            { ExpectedRevision = r.Revision, IsEditorSave = true, EditorPatch = r }, ct);
        if (r.Action == "UseStock")
            return await sender.Send(new UseProductionOrderStockCommand(command.Id, new()
            {
                TotalQuantity = merged.TotalQuantity, NumOfBatches = merged.NumOfBatches, StepOfProduct = merged.StepOfProduct,
                ExpectedDate = merged.ExpectedDate, LabNote = merged.LabNote, PlpuNote = merged.PlpuNote,
                Requirement = merged.Requirement, QcCheck = merged.QcCheck
            }) { ExpectedRevision = r.Revision, SourceFormulaId = r.SourceFormulaId }, ct);
        if (r.Action == "SaveAndCreateFormula")
        {
            if (!r.SourceFormulaId.HasValue || !r.Origin.HasValue || !visibility.CanViewFormulaMaterials())
                return OperationResult.Fail("Thiếu nguồn công thức hoặc quyền vật tư.");
            var result = await sender.Send(new CreateProductionOrderFormulaCommand(command.Id, new()
            {
                SourceFormulaId = r.SourceFormulaId.Value, Origin = r.Origin.Value, Items = r.Items,
                TotalQuantity = merged.TotalQuantity, NumOfBatches = merged.NumOfBatches, StepOfProduct = merged.StepOfProduct,
                ExpectedDate = merged.ExpectedDate, ManufacturingDate = merged.ManufacturingDate,
                LabNote = merged.LabNote, PlpuNote = merged.PlpuNote, Requirement = merged.Requirement, QcCheck = merged.QcCheck
            }) { ExpectedRevision = r.Revision }, ct);
            return result.Success ? OperationResult.Ok("Đã lưu và tạo công thức.") : OperationResult.Fail(result.Message ?? "Không thể tạo công thức.");
        }
        var action = r.Action == "Schedule" ? ManufacturingInformationAction.Schedule : ManufacturingInformationAction.RequestFormula;
        return await sender.Send(new UpdateProductionOrderInformationCommand(command.Id, new()
        {
            TotalQuantity = r.TotalQuantity ?? order.TotalQuantity, NumOfBatches = r.NumOfBatches ?? order.NumOfBatches,
            StepOfProduct = merged.StepOfProduct, ExpectedDate = merged.ExpectedDate,
            LabNote = merged.LabNote, PlpuNote = merged.PlpuNote, Requirement = merged.Requirement, QcCheck = merged.QcCheck
        }, action) { ExpectedRevision = r.Revision }, ct);
    }
}
