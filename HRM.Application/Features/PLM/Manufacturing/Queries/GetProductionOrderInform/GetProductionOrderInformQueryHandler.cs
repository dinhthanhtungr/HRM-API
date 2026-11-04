using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderFormulaSource;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderWorkspace;
using MediatR;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderInform;

internal sealed class GetProductionOrderInformQueryHandler(ISender sender, IManufacturingDbContext db,
    ICurrentUser user, ICurrentUserPermissionService permissions, IPLMFieldVisibilityService visibility)
    : IRequestHandler<GetProductionOrderInformQuery, OperationResult<ProductionOrderInformDto>>
{
    public async Task<OperationResult<ProductionOrderInformDto>> Handle(GetProductionOrderInformQuery request, CancellationToken ct)
    {
        var access = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (access is not null) return OperationResult<ProductionOrderInformDto>.Fail(access);
        var order = await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MfgProductionOrderId == request.Id
            && x.CompanyId == user.CompanyId && x.IsActive, ct);
        if (order is null) return OperationResult<ProductionOrderInformDto>.Fail("Không tìm thấy lệnh trong công ty.");
        var revision = await ManufacturingEditorRevision.ReadAsync(db, order, ct);
        var sampleInfo = await db.Products.AsNoTracking()
            .Where(x => x.ProductId == order.ProductId && x.CompanyId == user.CompanyId && x.IsActive)
            .Select(x => new ProductionOrderSampleInfoDto(x.ProductId,
                x.SampleRequests.Where(s => s.IsActive && s.CompanyId == user.CompanyId)
                    .OrderByDescending(s => s.CreatedDate).ThenByDescending(s => s.SampleRequestId)
                    .Select(s => (Guid?)s.SampleRequestId).FirstOrDefault(),
                x.SampleRequests.Where(s => s.IsActive && s.CompanyId == user.CompanyId)
                    .OrderByDescending(s => s.CreatedDate).ThenByDescending(s => s.SampleRequestId)
                    .Select(s => s.ExternalId).FirstOrDefault(),
                x.UsageRate, x.ExpiryType))
            .FirstOrDefaultAsync(ct);
        // Each composed query retains its own company/capability/field-visibility gates.
        var workspace = await sender.Send(new GetProductionOrderWorkspaceQuery(request.Id), ct);
        if (!workspace.Success || workspace.Data is null)
        {
            return OperationResult<ProductionOrderInformDto>.Fail(workspace.Message ?? "Không tìm thấy lệnh.");
        }

        var hasLink = await db.MfgOrderPOs.AsNoTracking().AnyAsync(x => x.MfgProductionOrderId == request.Id && x.IsActive
            && x.Detail.IsActive && x.Detail.MerchandiseOrder.IsActive && x.Detail.MerchandiseOrder.CompanyId == user.CompanyId, ct);
        var linkedOrderId = workspace.Data.Order.MerchandiseOrderId;
        var poNo = await db.MfgOrderPOs.AsNoTracking()
            .Where(x => x.MfgProductionOrderId == request.Id && x.IsActive && x.Detail.IsActive
                && x.Detail.MerchandiseOrder.IsActive && x.Detail.MerchandiseOrder.CompanyId == user.CompanyId
                && x.Detail.MerchandiseOrder.MerchandiseOrderId == linkedOrderId)
            .Select(x => x.Detail.MerchandiseOrder.PONo).FirstOrDefaultAsync(ct);
        var canManage = hasLink && user.EmployeeId is { } employee && employee != Guid.Empty
            && ManufacturingAccess.CanManageInform(user, permissions);
        var canEditFormula = hasLink && user.EmployeeId is { } labEmployee && labEmployee != Guid.Empty
            && (ManufacturingAccess.IsInformOperator(user) || user.IsInRole(ApplicationRoles.Lab.LabUser))
            && permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingFormulas);
        var canMaterials = visibility.CanViewFormulaMaterials();
        var actions = ManufacturingEditorPolicy.Actions(order.Status, canManage, canMaterials, workspace.Data.CanCreateFormula, canEditFormula);
        var sources = new List<ProductionOrderFormulaSourceDto>();
        var selectedId = workspace.Data.Order.ManufacturingFormulaIdIsSelect;
        if (canMaterials)
        {
            var ids = workspace.Data.FormulaOptions.Select(x => x.FormulaId)
                .Concat(selectedId.HasValue ? [selectedId.Value] : Array.Empty<Guid>()).Distinct();
            foreach (var formulaId in ids)
            {
                var source = await sender.Send(new GetProductionOrderFormulaSourceQuery(request.Id, formulaId), ct);
                if (!source.Success || source.Data is null)
                    return OperationResult<ProductionOrderInformDto>.Fail(source.Message ?? "Không thể đọc nguồn công thức. Vui lòng tải lại.");
                sources.Add(source.Data);
            }
        }
        var after = await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MfgProductionOrderId == request.Id && x.CompanyId == user.CompanyId && x.IsActive, ct);
        if (after is null || revision != await ManufacturingEditorRevision.ReadAsync(db, after, ct))
            return OperationResult<ProductionOrderInformDto>.Fail(ManufacturingEditorRevision.Conflict);
        return OperationResult<ProductionOrderInformDto>.Ok(new ProductionOrderInformDto(workspace.Data,
            sources.FirstOrDefault(x => x.FormulaId == selectedId))
        {
            SampleInfo = sampleInfo, PoNo = poNo, Revision = revision, AllowedActions = actions,
            EditableFields = actions.Length > 0
                ? (canManage ? ManufacturingEditorPolicy.HeaderFields : ManufacturingEditorPolicy.FormulaNoteFields)
                    .Where(x => x != "formulaNote" || canEditFormula && canMaterials && selectedId.HasValue)
                    .Where(x => x != "requirement" || permissions.HasPermission(ApplicationPermissions.PLM.EditProductionOrderRequirement)).ToArray()
                : [],
            CanEditItems = actions.Length > 0 && canMaterials,
            ReadOnlyReason = actions.Length > 0 ? null : "Không có quyền, thiếu liên kết active hoặc trạng thái chưa được nhận diện.",
            StepOptions = ManufacturingEditorPolicy.Steps, FormulaSources = sources
        });
    }
}
