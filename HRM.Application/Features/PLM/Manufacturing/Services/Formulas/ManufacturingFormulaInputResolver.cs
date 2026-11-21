using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Models;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

/// <summary>Resolve dữ liệu nguồn, giá, category và lot tại server; không nhận snapshot/cost từ FE.</summary>
internal sealed class ManufacturingFormulaInputResolver(IManufacturingDbContext db, IMaterialPriceQueryService prices, IDateTimeProvider clock,
    ICurrentUserPermissionService permissions)
{
    public async Task<string> BuildPlpuNoteAsync(
        MfgProductionOrder order,
        ProductionOrderFormulaOptionDto source,
        IReadOnlyList<ManufacturingFormulaInputItem>? input,
        IReadOnlyList<CreateProductionOrderFormulaItemRequest> resolved,
        string? manual,
        CancellationToken ct,
        bool allowUntrackedSourceRows = false
    )
    {
        var isVu = source.Origin is ManufacturingFormulaOrigin.FromVu or ManufacturingFormulaOrigin.Improvement;
        var originals = isVu ? await db.FormulaMaterials.AsNoTracking().Where(x => x.FormulaId == source.FormulaId && x.IsActive && x.Formula.CompanyId == order.CompanyId
            && x.Formula.IsActive)
            .OrderBy(x => x.LineNo)
            .Select(x => new NoteItem(x.FormulaMaterialId, x.itemType, x.MaterialId, x.ProductId, null))
            .ToListAsync(ct) : await db.ManufacturingFormulaMaterials.AsNoTracking().Where(x => x.ManufacturingFormulaId == source.FormulaId && x.IsActive
            && x.ManufacturingFormula.CompanyId == order.CompanyId
            && x.ManufacturingFormula.IsActive)
            .OrderBy(x => x.LineNo)
            .Select(x => new NoteItem(x.ManufacturingFormulaMaterialId, x.itemType, x.MaterialId, x.ProductId, x.LotNo))
            .ToListAsync(ct);
        var materialIds = originals.Where(x => ProductionOrderCreationRules.IsMaterial(x.Type)).Select(x => x.MaterialId)
            .ToArray();
        var productIds = originals.Where(x => !ProductionOrderCreationRules.IsMaterial(x.Type)).Select(x => x.ProductId)
            .ToArray();
        var codes = await db.Materials.AsNoTracking().Where(x => materialIds.Contains(x.MaterialId) && x.CompanyId == order.CompanyId && x.IsActive == true)
            .ToDictionaryAsync(x => x.MaterialId, x => x.ExternalId, ct);
        var productCodes = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.ProductId) && x.CompanyId == order.CompanyId && x.IsActive)
            .ToDictionaryAsync(
            x => x.ProductId,
            x => x.SampleRequests.Where(s => s.CompanyId == order.CompanyId && s.IsActive).OrderByDescending(s => s.CreatedDate)
            .Select(s => s.ExternalId)
            .FirstOrDefault(),
            ct
        );
        string Code(NoteItem item) => ProductionOrderCreationRules.IsMaterial(item.Type) ? codes.GetValueOrDefault(item.MaterialId ?? Guid.Empty) ?? string.Empty : productCodes.GetValueOrDefault(item.ProductId ?? Guid.Empty) ?? string.Empty;
        if (input is null)
        {
            return ManufacturingNoteRules.PreserveStoredPlpuAuto(manual, order.PlpuNote);
        }

        var changed = new List<string>();
        var added = new List<string>();
        var matchedOriginals = new HashSet<Guid>();
        foreach (var row in input)
        {
            var currentCode = resolved.FirstOrDefault(x => x.ItemId == row.ItemId
                && ProductionOrderCreationRules.IsMaterial(x.ItemType) == ProductionOrderCreationRules.IsMaterial(row.ItemType))?.MaterialExternalIdSnapshot ?? string.Empty;
            var original = originals.FirstOrDefault(x => x.Id == row.SourceItemId);
            if (original is null && allowUntrackedSourceRows)
            {
                var matches = originals.Where(x => (ProductionOrderCreationRules.IsMaterial(x.Type) ? x.MaterialId : x.ProductId) == row.ItemId
                    && ProductionOrderCreationRules.IsMaterial(x.Type) == ProductionOrderCreationRules.IsMaterial(row.ItemType)).ToList();
                if (matches.Count > 1)
                    matches = matches.Where(x => string.Equals(NormalizeLot(x.LotNo), NormalizeLot(row.LotNo), StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 1) original = matches[0];
            }
            if (original is not null) matchedOriginals.Add(original.Id);
            if (original is null)
            {
                added.Add(currentCode);
            }
            else if (!string.Equals(Code(original).Trim(), currentCode.Trim(), StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(Code(original))
                && !string.IsNullOrWhiteSpace(currentCode))
            {
                changed.Add($"{Code(original)} => {currentCode}");
            }
        }

        var requested = allowUntrackedSourceRows ? matchedOriginals
            : input.Where(x => x.SourceItemId.HasValue).Select(x => x.SourceItemId!.Value).ToHashSet();
        return ManufacturingNoteRules.ComposePlpu(manual, changed, added, originals.Where(x => !requested.Contains(x.Id)).Select(Code), clock.Now);
    }

    public async Task<OperationResult<List<CreateProductionOrderFormulaItemRequest>>> ResolveAsync(
        MfgProductionOrder order,
        ProductionOrderFormulaOptionDto source,
        IReadOnlyList<ManufacturingFormulaInputItem>? input,
        CancellationToken ct,
        bool allowZeroQuantity = false,
        bool includeInactiveSourceItems = false,
        bool refreshExistingPrices = false,
        bool refreshProductPrices = true,
        bool allowUntrackedSourceRows = false,
        bool preserveUnspecifiedLotQuality = false
    )
    {
        var isVu = source.Origin is ManufacturingFormulaOrigin.FromVu or ManufacturingFormulaOrigin.Improvement;
        var sourceItems = isVu ? await db.FormulaMaterials.AsNoTracking().Where(x => x.FormulaId == source.FormulaId && (x.IsActive || includeInactiveSourceItems)
            && x.Formula.CompanyId == order.CompanyId
            && x.Formula.IsActive)
            .OrderBy(x => x.LineNo)
            .Select(x => new SourceItem(
            x.FormulaMaterialId,
            x.itemType,
            x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure ? x.MaterialId ?? Guid.Empty : x.ProductId ?? Guid.Empty,
            x.Quantity,
            x.UnitPrice,
            x.CategoryId,
            x.Unit,
            null
        ))
            .ToListAsync(ct) : await db.ManufacturingFormulaMaterials.AsNoTracking().Where(x => x.ManufacturingFormulaId == source.FormulaId && (x.IsActive || includeInactiveSourceItems)
            && x.ManufacturingFormula.CompanyId == order.CompanyId
            && x.ManufacturingFormula.IsActive)
            .OrderBy(x => x.LineNo)
            .Select(x => new SourceItem(
            x.ManufacturingFormulaMaterialId,
            x.itemType,
            x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure ? x.MaterialId ?? Guid.Empty : x.ProductId ?? Guid.Empty,
            x.Quantity,
            x.UnitPrice,
            x.CategoryId,
            x.Unit,
            x.LotNo
        ))
            .ToListAsync(ct);
        var requested = input?.ToList() ?? sourceItems.Select(x => new ManufacturingFormulaInputItem
        {
            SourceItemId = x.Id,
            ItemId = x.ItemId,
            ItemType = x.Type,
            Quantity = x.Quantity,
            LotNo = x.LotNo
        })
            .ToList();
        if (requested.Any(x => x is not null && x.ItemType is ItemType.MaterialFailure or ItemType.ProductFailure)
            && !permissions.HasPermission(ApplicationPermissions.PLM.ViewManufacturingDefectiveLots))
            return Fail("Bạn không có quyền chọn hàng lỗi.");
        if (requested.Count == 0 || requested.Count > 1000
            || requested.Any(x => x is null || x.ItemId == Guid.Empty || !Enum.IsDefined(x.ItemType)
            || (allowZeroQuantity ? x.Quantity < 0 : x.Quantity <= 0)
            || x.Quantity > 1m + ProductionOrderCreationRules.FormulaRatioTolerance
            || x.LotNo?.Length > 100)
            || Math.Abs(requested.Sum(x => x.Quantity) - 1m) > ProductionOrderCreationRules.FormulaRatioTolerance)
        {
            return Fail("Công thức phải có thành phần hợp lệ và tổng tỷ lệ bằng 1.");
        }

        var sourceMap = sourceItems.ToDictionary(x => x.Id);
        if (!allowUntrackedSourceRows && requested.Any(x => x.SourceItemId.HasValue && !sourceMap.ContainsKey(x.SourceItemId.Value)))
        {
            return Fail("Dòng công thức không thuộc nguồn đang chọn.");
        }

        var provenance = requested.Where(x => x.SourceItemId.HasValue).Select(x => x.SourceItemId!.Value).ToList();
        if (!allowUntrackedSourceRows && provenance.Distinct().Count() != provenance.Count)
        {
            return Fail("Một dòng nguồn không được sử dụng nhiều lần.");
        }

        if (allowUntrackedSourceRows)
        {
            // Khi tạo VA, ID dòng chỉ hỗ trợ đối chiếu. Chỉ suy nguồn từ dữ liệu đã
            // scope theo công thức/công ty; không đọc dòng ngoài nguồn để lấy giá/lot.
            requested = requested.Select(row =>
            {
                var original = row.SourceItemId.HasValue ? sourceMap.GetValueOrDefault(row.SourceItemId.Value) : null;
                if (original is null)
                {
                    var matches = sourceItems.Where(x => x.ItemId == row.ItemId
                        && ProductionOrderCreationRules.IsMaterial(x.Type) == ProductionOrderCreationRules.IsMaterial(row.ItemType)).ToList();
                    if (matches.Count > 1)
                        matches = matches.Where(x => string.Equals(NormalizeLot(x.LotNo), NormalizeLot(row.LotNo), StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matches.Count == 1) original = matches[0];
                }
                return new ManufacturingFormulaInputItem { SourceItemId = original?.Id,
                    ItemId = row.ItemId, ItemType = row.ItemType, Quantity = row.Quantity, LotNo = row.LotNo };
            }).ToList();
        }

        var prepared = false;
        for (var i = 0; i < requested.Count; i++)
        {
            var row = requested[i];
            if (ProductionOrderCreationRules.IsMaterial(row.ItemType)) continue;
            try
            {
                var product = await ProcessingPreviewIdentity.ResolveAsync(db, row.ItemId, order.CompanyId,
                    order.UpdatedBy != Guid.Empty ? order.UpdatedBy : order.CreatedBy, clock.Now, ct);
                if (product is null) continue;
                requested[i] = new ManufacturingFormulaInputItem { ItemId = product.ProductId, ItemType = row.ItemType,
                    SourceItemId = row.SourceItemId, Quantity = row.Quantity, LotNo = row.LotNo };
                prepared = true;
            }
            catch (InvalidOperationException ex) { return Fail(ex.Message); }
        }
        // The caller owns the transaction; make default formula/pricing available to the shared resolver.
        if (prepared) await db.SaveChangesAsync(ct);
        var materialIds = requested.Where(x => ProductionOrderCreationRules.IsMaterial(x.ItemType)).Select(x => x.ItemId)
            .Distinct()
            .ToArray();
        var productIds = requested.Where(x => !ProductionOrderCreationRules.IsMaterial(x.ItemType)).Select(x => x.ItemId)
            .Distinct()
            .ToArray();
        var materials = await db.Materials.AsNoTracking().Where(x => materialIds.Contains(x.MaterialId) && x.CompanyId == order.CompanyId && x.IsActive == true)
            .Select(x => new ItemData(x.MaterialId, x.ExternalId, x.Name, x.CategoryId, x.Unit))
            .ToDictionaryAsync(x => x.Id, ct);
        var products = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.ProductId) && x.CompanyId == order.CompanyId && x.IsActive)
            .Select(x => new { x.ProductId, x.ColourCode, x.Name, x.CategoryId, x.Unit, SampleCode = x.SampleRequests.Where(s => s.IsActive && s.CompanyId == order.CompanyId).OrderByDescending(s => s.CreatedDate)
            .Select(s => s.ExternalId)
            .FirstOrDefault() })
            .ToListAsync(ct);
        var productMap = products.ToDictionary(x => x.ProductId, x => new ItemData(x.ProductId, x.SampleCode, x.Name, x.CategoryId, x.Unit));
        var stockCodes = materials.Values.Select(x => x.Code).Concat(products.Select(x => x.ColourCode)).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();
        var lotNames = requested.Select(x => NormalizeLot(x.LotNo)).Where(x => x is not null).Distinct().ToArray();
        var lots = await db.WarehouseShelfStocks.AsNoTracking().Where(x => x.CompanyId == order.CompanyId && x.WarehouseShelves != null
            && x.WarehouseShelves.CompanyId == order.CompanyId
            && x.WarehouseShelves.IsActive
            && stockCodes.Contains(x.Code.Trim().ToUpper())
            && lotNames.Contains(x.LotNo))
            .Select(x => new { x.Code, x.LotNo, x.StockType })
            .Distinct()
            .ToListAsync(ct);
        var newRows = requested.Where(x => !x.SourceItemId.HasValue || !sourceMap.TryGetValue(x.SourceItemId.Value, out var old)
            || old.ItemId != x.ItemId
            || ProductionOrderCreationRules.IsMaterial(old.Type) != ProductionOrderCreationRules.IsMaterial(x.ItemType))
            .ToList();
        var priceRows = refreshExistingPrices ? requested.Where(x => refreshProductPrices || ProductionOrderCreationRules.IsMaterial(x.ItemType) || newRows.Contains(x))
            .ToList() : newRows;
        var latestPrices = priceRows.Count == 0 ? new Dictionary<PriceItemKey, LatestItemPriceDto>() : await prices.LoadLatestPricingItemPriceInfoDictAsync(
            order.CompanyId,
            "VND",
            priceRows.Select(x => new PriceItemRequest
            {
                ItemType = ProductionOrderCreationRules.IsMaterial(x.ItemType) ? ItemType.Material : ItemType.Product,
                MaterialId = ProductionOrderCreationRules.IsMaterial(x.ItemType) ? x.ItemId : null,
                ProductId = ProductionOrderCreationRules.IsMaterial(x.ItemType) ? null : x.ItemId
            }),
            ct
        );
        var result = new List<CreateProductionOrderFormulaItemRequest>();
        foreach (var row in requested)
        {
            var material = ProductionOrderCreationRules.IsMaterial(row.ItemType);
            var data = (material ? materials : productMap).GetValueOrDefault(row.ItemId);
            if (data is null)
            {
                return Fail("Không tìm thấy vật tư/sản phẩm active trong công ty.");
            }

            var old = row.SourceItemId.HasValue ? sourceMap.GetValueOrDefault(row.SourceItemId.Value) : null;
            var sameItem = old is not null && old.ItemId == row.ItemId
                && ProductionOrderCreationRules.IsMaterial(old.Type) == material;
            var baseType = material ? ItemType.Material : ItemType.Product;
            decimal price;
            if (sameItem)
            {
                price = refreshExistingPrices && (material || refreshProductPrices)
                    && latestPrices.TryGetValue(new(baseType, row.ItemId), out var refreshed) ? refreshed.CurrentPrice : old!.Price;
            }
            else if (latestPrices.TryGetValue(new(baseType, row.ItemId), out var currentPrice))
            {
                price = currentPrice.CurrentPrice;
            }
            else
            {
                return Fail("Không có nguồn giá hợp lệ cho vật tư/sản phẩm mới.");
            }

            var lotName = NormalizeLot(row.LotNo);
            ProductionOrderLotNumberDto? lot = null;
            if (lotName is not null)
            {
                var stockCode = material ? data.Code : products.First(x => x.ProductId == row.ItemId).ColourCode;
                var matching = lots.Where(x => string.Equals(x.Code.Trim(), stockCode?.Trim(), StringComparison.OrdinalIgnoreCase)
                    && x.LotNo == lotName)
                    .ToList();
                if (matching.Count > 1)
                    matching = matching.Where(x => ProductionOrderCreationRules.ResolveItemType(baseType,
                        new ProductionOrderLotNumberDto { StockType = x.StockType }) == row.ItemType).ToList();
                if (matching.Count == 0 && sameItem
                    && string.Equals(NormalizeLot(old!.LotNo), lotName, StringComparison.OrdinalIgnoreCase))
                {
                    // A saved lot may have no remaining stock. The scoped source row is provenance;
                    // keep its saved quality type rather than rejecting a legacy formula copy/edit.
                    lot = new()

                    {

                        LotNo = lotName,

                        StockType = old.Type switch
                        {
                            ItemType.MaterialFailure => StockType.DefectiveRawMaterial,
                            ItemType.ProductFailure => StockType.DefectiveFinishedGood,
                            ItemType.Material => StockType.RawMaterial,
                            _ => StockType.FinishedGood
                        }

                    };
                }
                else
                {
                    if (matching.Count != 1)
                    {
                        var quality = row.ItemType is ItemType.ProductFailure or ItemType.MaterialFailure ? "Hàng lỗi" : "Hàng đạt";
                        var reason = matching.Count == 0 ? "Không tìm thấy lot trên kệ active của đúng vật tư/sản phẩm trong công ty."
                            : "Lot có nhiều tình trạng kho không nhất quán với loại hàng đã chọn.";
                        return Fail($"[formulaItem:{row.ItemId:D}:{(int)row.ItemType}:{Uri.EscapeDataString(lotName)}] "
                            + $"Dòng NVL {requested.IndexOf(row) + 1} — {data.Code} [{stockCode}] — Lot '{lotName}' — {quality}: {reason}");
                    }

                    lot = new()

                    {

                        LotNo = lotName,

                        StockType = matching[0].StockType

                    };
                }
            }

            result.Add(new()
            {
                ItemId = row.ItemId,
                ItemType = lot is null && preserveUnspecifiedLotQuality ? row.ItemType : baseType,
                Quantity = row.Quantity,
                UnitPrice = price,
                CategoryId = sameItem ? old!.CategoryId : data.CategoryId,
                Unit = sameItem ? old!.Unit : data.Unit,
                MaterialExternalIdSnapshot = data.Code,
                MaterialNameSnapshot = data.Name,
                LotNumber = lot,
                IsActive = true,
                LineNo = result.Count + 1
            });
        }

        var categoryIds = result.Select(x => x.CategoryId).Distinct().ToArray();
        if (await db.Categories.AsNoTracking().CountAsync(x => categoryIds.Contains(x.CategoryId) && x.CompanyId == order.CompanyId && x.IsActive == true, ct) != categoryIds.Length)
        {
            return Fail("Nhóm vật tư không hợp lệ trong công ty.");
        }

        foreach (var row in result)
        {
            if (row.LotNumber is not null || !preserveUnspecifiedLotQuality)
                row.ItemType = ProductionOrderCreationRules.ResolveItemType(row.ItemType, row.LotNumber);
            if (row.ItemType is ItemType.MaterialFailure or ItemType.ProductFailure
                && !permissions.HasPermission(ApplicationPermissions.PLM.ViewManufacturingDefectiveLots))
                return Fail("Bạn không có quyền chọn hàng lỗi.");
        }

        if (ManufacturingFormulaInputRules.HasDuplicates(result))
        {
            return Fail("Công thức có dòng vật tư/lot trùng nhau.");
        }

        return OperationResult<List<CreateProductionOrderFormulaItemRequest>>.Ok(ManufacturingFormulaInputRules.OrderItems(result));
    }

    private static string? NormalizeLot(string? lot) => string.IsNullOrWhiteSpace(lot)
        || string.Equals(lot.Trim(), "N/A", StringComparison.OrdinalIgnoreCase) ? null : lot.Trim();
    private static OperationResult<List<CreateProductionOrderFormulaItemRequest>> Fail(string message) => OperationResult<List<CreateProductionOrderFormulaItemRequest>>.Fail(message);
    private sealed record SourceItem(
        Guid Id,
        ItemType Type,
        Guid ItemId,
        decimal Quantity,
        decimal Price,
        Guid CategoryId,
        string? Unit,
        string? LotNo
    );
    private sealed record ItemData(
        Guid Id,
        string? Code,
        string? Name,
        Guid CategoryId,
        string? Unit
    );
    private sealed record NoteItem(
        Guid Id,
        ItemType Type,
        Guid? MaterialId,
        Guid? ProductId,
        string? LotNo
    );
}
