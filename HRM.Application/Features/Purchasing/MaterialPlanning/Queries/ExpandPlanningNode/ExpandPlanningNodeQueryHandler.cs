using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Application.Features.Purchasing.MaterialPlanning.Dtos;
using HRM.Application.Features.Purchasing.MaterialPlanning.Services;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.MaterialPlanning.Queries.ExpandPlanningNode;

/// <summary>Preview một cấp công thức từ dòng đơn bán; tự kiểm tra đường dẫn, không tạo reservation/chứng từ.</summary>
internal sealed class ExpandPlanningNodeQueryHandler(IPLMReadDbContext db, IWarehouseReadDbContext warehouse,
    ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<ExpandPlanningNodeQuery, OperationResult<PlanningExpansionDto>>
{
    private const int MaxDepth = 10;
    private const int MaxChildren = 200;

    public async Task<OperationResult<PlanningExpansionDto>> Handle(ExpandPlanningNodeQuery request, CancellationToken ct)
    {
        if (!MaterialPlanningScope.CanRead(user, permissions) ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders) ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials))
            return Fail("Không có quyền xem nguồn nhu cầu/công thức hoặc thiếu công ty.");
        if (request.Path is null || request.Path.Length > MaxDepth || request.Path.Any(x => x == Guid.Empty) ||
            request.OrderId == Guid.Empty || request.LineId == Guid.Empty || request.ProductionOrderId == Guid.Empty)
            return Fail("Đường dẫn hoặc mã đơn/lệnh không hợp lệ.");
        var company = user.CompanyId!.Value;
        var delivered = DeliveryPlanSourceQuery.CountedAsDelivered(db.DeliveryOrderDetails.AsNoTracking(), company);
        var line = await MaterialPlanningScope.Lines(db, company)
            .Where(x => x.MerchandiseOrderId == request.OrderId && x.MerchandiseOrderDetailId == request.LineId)
            .Select(x => new { x.ProductId, x.FormulaId, Quantity = Math.Max(0m, x.ExpectedQuantity -
                (delivered.Where(d => d.MerchandiseOrderDetailId == x.MerchandiseOrderDetailId)
                    .Sum(d => (decimal?)d.Quantity) ?? 0m)) }).FirstOrDefaultAsync(ct);
        if (line is null) return Fail("Không tìm thấy dòng đơn trong phạm vi lập nhu cầu.");
        var links = await DeliveryPlanSourceQuery.ActiveLinks(db.MfgOrderPOs.AsNoTracking(), company)
            .Where(x => x.MerchandiseOrderDetailId == request.LineId && x.ProductionOrder.ProductId == line.ProductId)
            .Select(x => x.MfgProductionOrderId).Distinct().ToListAsync(ct);
        Guid? mfg = request.ProductionOrderId;
        if (mfg.HasValue && !links.Contains(mfg.Value)) return Fail("Lệnh sản xuất không thuộc dòng đơn.");
        if (!mfg.HasValue)
        {
            if (links.Count > 1) return Ok(null, line.Quantity, "SelectProductionOrder");
            mfg = links.Count == 1 ? links[0] : null;
        }
        var product = line.ProductId;
        var quantity = line.Quantity;
        var visited = new HashSet<Guid> { product };
        Guid? orderFormulaId = line.FormulaId == Guid.Empty ? null : line.FormulaId;
        PlanningFormulaDto? formula;
        foreach (var sourceLine in request.Path)
        {
            if (!await ActiveProduct(product, company, ct)) return Ok(null, quantity, "ProductUnavailable");
            formula = await PlanningFormulaReader.ReadAsync(db, company, product, mfg, ct, orderFormulaId);
            if (formula is null) return Ok(null, quantity, "MissingFormula");
            var child = await PlanningFormulaReader.Items(db, formula)
                .Where(x => x.Id == sourceLine && x.Type == ItemType.Product && x.Quantity > 0)
                .Select(x => new { x.ProductId, x.Quantity, x.Unit }).FirstOrDefaultAsync(ct);
            if (child?.ProductId is not { } childId) return Fail("Đường dẫn công thức không còn hợp lệ, hãy tải lại cây.");
            if (!IsKg(child.Unit)) return Ok(formula, quantity, "UnsupportedUnit");
            if (!visited.Add(childId)) return Ok(formula, quantity, "CycleDetected");
            product = childId;
            if (!TryMultiply(quantity, child.Quantity, out quantity)) return Fail("Khối lượng vượt giới hạn tính toán.");
            mfg = null;
            orderFormulaId = null;
        }
        if (!await ActiveProduct(product, company, ct)) return Ok(null, quantity, "ProductUnavailable");
        formula = await PlanningFormulaReader.ReadAsync(db, company, product, mfg, ct, orderFormulaId);
        if (formula is null) return Ok(null, quantity, "MissingFormula");
        var rows = await PlanningFormulaReader.Items(db, formula)
            .OrderBy(x => x.LineNo).ThenBy(x => x.Id).Take(MaxChildren + 1).ToListAsync(ct);
        if (rows.Count > MaxChildren) return Ok(formula, quantity, "NodeLimitExceeded");
        var sources = rows.Select(x => new FormulaItemDisplaySource(
            x.Type == ItemType.Material ? x.MaterialId ?? Guid.Empty : x.ProductId ?? Guid.Empty,
            x.Type, x.MaterialNameSnapshot, x.MaterialExternalIdSnapshot)).ToArray();
        var displays = await FormulaItemDisplayResolver.LoadCurrentDataAsync(db, company, sources, ct);
        var productIds = rows.Where(x => x.Type == ItemType.Product).Select(x => x.ProductId).ToArray();
        var products = await db.Products.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && productIds.Contains(x.ProductId))
            .Select(x => new { x.ProductId, x.ColourCode }).ToDictionaryAsync(x => x.ProductId, ct);
        var materialIds = rows.Where(x => x.Type == ItemType.Material).Select(x => x.MaterialId).ToArray();
        var materials = await db.Materials.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive == true && materialIds.Contains(x.MaterialId))
            .Select(x => new { x.MaterialId, x.ExternalId }).ToDictionaryAsync(x => x.MaterialId, ct);
        var stock = await MaterialPlanningStockReader.ReadAsync(warehouse, company,
            products.Values.Select(x => x.ColourCode ?? "").Concat(materials.Values.Select(x => x.ExternalId ?? "")).ToArray(), ct);
        var nodes = new List<PlanningNodeDto>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var material = row.Type == ItemType.Material;
            var id = material ? row.MaterialId ?? Guid.Empty : row.ProductId ?? Guid.Empty;
            var supported = row.Type is ItemType.Material or ItemType.Product;
            var active = supported && (material ? materials.ContainsKey(id) : products.ContainsKey(id));
            var display = FormulaItemDisplayResolver.Resolve(sources[i], displays);
            var issue = !supported ? "UnsupportedItemType" : !active ? "ItemUnavailable" : !IsKg(row.Unit) ? "UnsupportedUnit" :
                row.Quantity <= 0 ? "InvalidQuantity" : !material && visited.Contains(id) ? "CycleDetected" :
                !material && request.Path.Length >= MaxDepth ? "DepthLimitReached" : null;
            if (!TryMultiply(quantity, row.Quantity, out var required)) return Fail("Khối lượng vượt giới hạn tính toán.");
            var code = active ? material ? materials[id].ExternalId : products[id].ColourCode : null;
            nodes.Add(new(id, row.Id, row.Type, display.ExternalId, display.Name,
                supported && IsKg(row.Unit) && row.Quantity > 0 ? required : null,
                active ? MaterialPlanningStockReader.Get(stock, code, material) : 0m, !material && issue is null, issue));
        }
        return OperationResult<PlanningExpansionDto>.Ok(new PlanningExpansionDto(formula, quantity,
            rows.Count == 0 ? "EmptyFormula" : null, nodes));
    }

    private Task<bool> ActiveProduct(Guid id, Guid company, CancellationToken ct)
        => db.Products.AsNoTracking().AnyAsync(x => x.ProductId == id && x.CompanyId == company && x.IsActive, ct);

    private static bool IsKg(string? unit) => string.Equals(unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase);
    private static bool TryMultiply(decimal left, decimal right, out decimal result)
    {
        try { result = checked(left * right); return true; }
        catch (OverflowException) { result = 0; return false; }
    }
    private static OperationResult<PlanningExpansionDto> Fail(string error) => OperationResult<PlanningExpansionDto>.Fail(error);
    private static OperationResult<PlanningExpansionDto> Ok(PlanningFormulaDto? formula, decimal quantity, string issue)
        => OperationResult<PlanningExpansionDto>.Ok(new PlanningExpansionDto(formula, quantity, issue, []));
}
