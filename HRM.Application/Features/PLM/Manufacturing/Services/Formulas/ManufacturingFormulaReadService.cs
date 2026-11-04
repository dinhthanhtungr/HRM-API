using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

internal sealed class ManufacturingFormulaReadService(
    IManufacturingDbContext db,
    IPLMReadDbContext displayDb,
    IPLMFieldVisibilityService visibility,
    IMaterialPriceQueryService prices
)
{
    // Caller proves the formula's association to the requested MFG/product. This reader
    // still scopes parent references, batches current display data and masks costs in SQL.
    public async Task<IReadOnlyList<ProductionOrderFormulaSourceItemDto>> ReadAsync(
        Guid company,
        Guid formulaId,
        bool vu,
        bool includeInactive,
        bool latestProductPrices,
        CancellationToken ct
    ) => (await ReadBatchAsync(
        company,
        vu ? [] : [formulaId],
        vu ? [formulaId] : [],
        includeInactive,
        latestProductPrices,
        true,
        ct
    )).GetValueOrDefault((formulaId, vu)) ?? [];
    public async Task<IReadOnlyDictionary<(Guid Id, bool Vu), IReadOnlyList<ProductionOrderFormulaSourceItemDto>>> ReadBatchAsync(
        Guid company,
        Guid[] vaIds,
        Guid[] vuIds,
        bool includeInactive,
        bool latestProductPrices,
        bool includeStock,
        CancellationToken ct,
        bool includePrices = true,
        bool refreshPrices = true
    )
    {
        var priceVisible = includePrices && visibility.CanViewFormulaPrices();
        var vuRows = await db.FormulaMaterials.AsNoTracking().Where(x => vuIds.Contains(x.FormulaId) && x.Formula.CompanyId == company && (includeInactive || x.IsActive))
            .OrderBy(x => x.LineNo == 0 ? int.MaxValue : x.LineNo)
            .ThenBy(x => x.MaterialExternalIdSnapshot)
            .Select(x => new FormulaRow(
            x.FormulaId,
            true,
            new ProductionOrderFormulaSourceItemDto(
                x.FormulaMaterialId,
                x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure ? x.MaterialId ?? Guid.Empty : x.ProductId ?? Guid.Empty,
                x.itemType,
                x.CategoryId,
                x.LineNo,
                x.Quantity,
                priceVisible ? x.UnitPrice : null,
                x.MaterialNameSnapshot,
                x.MaterialExternalIdSnapshot,
                x.Unit,
                null
            )
        ))
            .ToListAsync(ct);
        var vaRows = await db.ManufacturingFormulaMaterials.AsNoTracking().Where(x => vaIds.Contains(x.ManufacturingFormulaId) && x.ManufacturingFormula.CompanyId == company
            && (includeInactive || x.IsActive))
            .OrderBy(x => x.LineNo == 0 ? int.MaxValue : x.LineNo)
            .ThenBy(x => x.MaterialExternalIdSnapshot)
            .Select(x => new FormulaRow(
            x.ManufacturingFormulaId,
            false,
            new ProductionOrderFormulaSourceItemDto(
                x.ManufacturingFormulaMaterialId,
                x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure ? x.MaterialId ?? Guid.Empty : x.ProductId ?? Guid.Empty,
                x.itemType,
                x.CategoryId,
                x.LineNo,
                x.Quantity,
                priceVisible ? x.UnitPrice : null,
                x.MaterialNameSnapshot,
                x.MaterialExternalIdSnapshot,
                x.Unit,
                x.LotNo
            )
        ))
            .ToListAsync(ct);
        var raw = vuRows.Concat(vaRows).ToList();
        var rows = raw.Select(x => x.Item).ToList();
        var displays = await FormulaItemDisplayResolver.LoadCurrentDataAsync(
            displayDb,
            company,
            rows.Select(x => new FormulaItemDisplaySource(x.ItemId, x.ItemType, x.Name, x.Code)),
            ct
        );
        var latest = priceVisible && refreshPrices ? await prices.LoadLatestPricingItemPriceInfoDictAsync(
            company,
            "VND",
            rows.Where(x => latestProductPrices || Material(x.ItemType)).Select(x => new PriceItemRequest
            {
                ItemType = Material(x.ItemType) ? ItemType.Material : ItemType.Product,
                MaterialId = Material(x.ItemType) ? x.ItemId : null,
                ProductId = Material(x.ItemType) ? null : x.ItemId
            }),
            ct
        ) : new Dictionary<PriceItemKey, LatestItemPriceDto>();
        var items = rows.Select(x =>
        {
            var display = FormulaItemDisplayResolver.Resolve(new(x.ItemId, x.ItemType, x.Name, x.Code), displays);
            var current = latest.GetValueOrDefault(new PriceItemKey(Material(x.ItemType) ? ItemType.Material : ItemType.Product, x.ItemId));
            return x with

            {

                Name = display.Name,

                Code = display.ExternalId,

                UnitPrice = current?.CurrentPrice ?? x.UnitPrice,

                PriceDate = priceVisible ? current?.PriceDate : null

            };
        })
            .ToList();
        if (includeStock)
        {
            items = await ManufacturingFormulaStockReader.FillAsync(db, company, items, ct, visibility.CanViewManufacturingDefectiveLots());
        }

        return raw.Select((x, index) => new { x.Id, x.Vu, Item = items[index] }).GroupBy(x => (x.Id, x.Vu))
            .ToDictionary(x => x.Key, x => (IReadOnlyList<ProductionOrderFormulaSourceItemDto>)x.Select(s => s.Item).ToList());
    }

    private static bool Material(ItemType type) => type is ItemType.Material or ItemType.MaterialFailure;
    private sealed record FormulaRow(
        Guid Id,
        bool Vu,
        ProductionOrderFormulaSourceItemDto Item
    );
}
