using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Services;

/// <summary>Nhận diện dòng công thức một thành phần để tô ô số lượng như phiếu legacy; không đọc giá/material DTO.</summary>
internal sealed class DeliveryDocumentFormulaService(IDispatchReadDbContext db)
{
    public async Task EnrichAsync(DeliveryOrderDocumentDto document, Guid companyId, CancellationToken ct)
    {
        var detailIds = document.Lines.Where(x => x.MerchandiseOrderDetailId.HasValue)
            .Select(x => x.MerchandiseOrderDetailId!.Value).Distinct().ToArray();
        var selected = await db.MfgOrderPOs.AsNoTracking().Where(x => x.IsActive && detailIds.Contains(x.MerchandiseOrderDetailId) &&
            x.ProductionOrder.IsActive && x.ProductionOrder.CompanyId == companyId && x.Detail.MerchandiseOrder.CompanyId == companyId)
            .SelectMany(x => x.ProductionOrder.ProductionSelectVersions.Where(v => v.ValidTo == null && v.ManufacturingFormula != null &&
                v.ManufacturingFormula.CompanyId == companyId && v.ManufacturingFormula.IsActive), (x, v) => new
                { DetailId = x.MerchandiseOrderDetailId, Code = v.ManufacturingFormula!.ExternalId }).ToListAsync(ct);
        var selectedByDetail = selected.GroupBy(x => x.DetailId).ToDictionary(g => g.Key, g => g.Select(x => x.Code).ToArray());
        var codes = document.Lines.SelectMany(x => Split(x.LotNo)).Concat(selected.Select(x => x.Code))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var singleCodes = (await db.ManufacturingFormulas.AsNoTracking().Where(x => x.IsActive && x.CompanyId == companyId &&
            codes.Contains(x.ExternalId) && x.ManufacturingFormulaMaterials.Count(m => m.IsActive) == 1)
            .Select(x => x.ExternalId).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var productIds = document.Lines.Where(x => x.ProductId.HasValue &&
                (!x.MerchandiseOrderDetailId.HasValue || !selectedByDetail.ContainsKey(x.MerchandiseOrderDetailId.Value)))
            .Select(x => x.ProductId!.Value).Distinct().ToArray();
        var singleProducts = (await db.Formulas.AsNoTracking().Where(x => x.IsActive && x.CompanyId == companyId &&
            productIds.Contains(x.ProductId) && x.FormulaMaterials.Count(m => m.IsActive) == 1)
            .Select(x => x.ProductId).Distinct().ToListAsync(ct)).ToHashSet();
        foreach (var line in document.Lines)
        {
            var selectedCodes = line.MerchandiseOrderDetailId.HasValue
                ? selectedByDetail.GetValueOrDefault(line.MerchandiseOrderDetailId.Value) ?? [] : [];
            line.IsSingleMaterialFormula = Split(line.LotNo).Concat(selectedCodes).Any(singleCodes.Contains) ||
                (selectedCodes.Length == 0 && line.ProductId.HasValue && singleProducts.Contains(line.ProductId.Value));
        }
    }

    private static IEnumerable<string> Split(string? value) => (value ?? string.Empty)
        .Split([',', ';', '|', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0);
}
