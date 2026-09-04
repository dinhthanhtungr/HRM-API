using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.Commons.Pricing;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Pricing.Rules;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.Services.Pricing
{
    public class MaterialPriceQueryService : IMaterialPriceQueryService
    {
        private readonly IPriceReadDbContext _dbContext;

        public MaterialPriceQueryService(IPriceReadDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestMaterialPriceInfoDictAsync(
            IEnumerable<Guid?> materialIds,
            CancellationToken cancellationToken = default)
        {
            var ids = NormalizeIds(materialIds);

            if (ids.Count == 0)
            {
                return new Dictionary<Guid, LatestMaterialPriceDto>();
            }

            var latestPoRows = await _dbContext.PurchaseOrderDetails
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    ids.Contains(x.MaterialId) &&
                    x.PurchaseOrder != null &&
                    (x.PurchaseOrder.IsActive ?? true) &&
                    (x.PurchaseOrder.Status == null ||
                     !PurchaseOrderPriceRules.CanceledStatuses.Contains(x.PurchaseOrder.Status)))
                .GroupBy(x => x.MaterialId)
                .Select(g => g
                    .OrderByDescending(x => x.PurchaseOrder!.CreateDate)
                    .ThenByDescending(x => x.LineNo)
                    .Select(x => new
                    {
                        x.MaterialId,
                        Price = x.UnitPriceAgreed,
                        PriceDate = (DateTime?)x.PurchaseOrder!.CreateDate
                    })
                    .FirstOrDefault()!)
                .ToListAsync(cancellationToken);

            var poDict = latestPoRows.ToDictionary(
                x => x.MaterialId,
                x => new MaterialPriceCandidate
                {
                    MaterialId = x.MaterialId,
                    CurrentPrice = x.Price ?? 0m,
                    HasPriceValue = x.Price.HasValue,
                    PriceDate = x.PriceDate,
                    PriceSource = MaterialPriceSource.PurchaseOrder
                });

            var supplierRows = await _dbContext.MaterialsSuppliers
                .AsNoTracking()
                .Where(s =>
                    (s.IsActive ?? true) &&
                    ids.Contains(s.MaterialId))
                .GroupBy(s => s.MaterialId)
                .Select(g => g
                    .OrderByDescending(x => x.UpdatedDate ?? x.CreateDate)
                    .ThenByDescending(x => x.IsPreferred ?? false)
                    .Select(x => new
                    {
                        x.MaterialId,
                        x.CurrentPrice,
                        PriceDate = x.UpdatedDate ?? x.CreateDate
                    })
                    .FirstOrDefault()!)
                .ToListAsync(cancellationToken);

            var supplierDict = supplierRows.ToDictionary(
                x => x.MaterialId,
                x => new MaterialPriceCandidate
                {
                    MaterialId = x.MaterialId,
                    CurrentPrice = x.CurrentPrice ?? 0m,
                    HasPriceValue = x.CurrentPrice.HasValue,
                    PriceDate = x.PriceDate,
                    PriceSource = MaterialPriceSource.MaterialSupplier
                });

            return MaterialLatestPriceSelector.SelectMany(ids, poDict, supplierDict);
        }

        public async Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestMaterialPriceInfoBySupplierDictAsync(
            Guid supplierId,
            IEnumerable<Guid?> materialIds,
            CancellationToken cancellationToken = default)
        {
            var ids = NormalizeIds(materialIds);

            if (ids.Count == 0 || supplierId == Guid.Empty)
            {
                return new Dictionary<Guid, LatestMaterialPriceDto>();
            }

            var latestPoRows = await _dbContext.PurchaseOrderDetails
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    ids.Contains(x.MaterialId) &&
                    x.PurchaseOrder != null &&
                    x.PurchaseOrder.SupplierId == supplierId &&
                    (x.PurchaseOrder.IsActive ?? true) &&
                    (x.PurchaseOrder.Status == null ||
                     !PurchaseOrderPriceRules.CanceledStatuses.Contains(x.PurchaseOrder.Status)))
                .GroupBy(x => x.MaterialId)
                .Select(g => g
                    .OrderByDescending(x => x.PurchaseOrder!.CreateDate)
                    .ThenByDescending(x => x.LineNo)
                    .Select(x => new
                    {
                        x.MaterialId,
                        Price = x.UnitPriceAgreed,
                        PriceDate = (DateTime?)x.PurchaseOrder!.CreateDate
                    })
                    .FirstOrDefault()!)
                .ToListAsync(cancellationToken);

            var poDict = latestPoRows.ToDictionary(
                x => x.MaterialId,
                x => new MaterialPriceCandidate
                {
                    MaterialId = x.MaterialId,
                    CurrentPrice = x.Price ?? 0m,
                    HasPriceValue = x.Price.HasValue,
                    PriceDate = x.PriceDate,
                    PriceSource = MaterialPriceSource.PurchaseOrder
                });

            var supplierRows = await _dbContext.MaterialsSuppliers
                .AsNoTracking()
                .Where(s =>
                    (s.IsActive ?? true) &&
                    s.SupplierId == supplierId &&
                    ids.Contains(s.MaterialId))
                .GroupBy(s => s.MaterialId)
                .Select(g => g
                    .OrderByDescending(x => x.UpdatedDate ?? x.CreateDate)
                    .ThenByDescending(x => x.IsPreferred ?? false)
                    .Select(x => new
                    {
                        x.MaterialId,
                        x.CurrentPrice,
                        PriceDate = x.UpdatedDate ?? x.CreateDate
                    })
                    .FirstOrDefault()!)
                .ToListAsync(cancellationToken);

            var supplierDict = supplierRows.ToDictionary(
                x => x.MaterialId,
                x => new MaterialPriceCandidate
                {
                    MaterialId = x.MaterialId,
                    CurrentPrice = x.CurrentPrice ?? 0m,
                    HasPriceValue = x.CurrentPrice.HasValue,
                    PriceDate = x.PriceDate,
                    PriceSource = MaterialPriceSource.MaterialSupplier
                });

            return MaterialLatestPriceSelector.SelectMany(ids, poDict, supplierDict);
        }

        public async Task<Dictionary<PriceItemKey, LatestItemPriceDto>> LoadLatestItemPriceInfoDictAsync(
            IEnumerable<PriceItemRequest> items,
            CancellationToken cancellationToken = default)
        {
            var materialIds = items
                .Where(x => x.ItemType == ItemType.Material &&
                            x.MaterialId.HasValue &&
                            x.MaterialId.Value != Guid.Empty)
                .Select(x => x.MaterialId!.Value)
                .Distinct()
                .ToList();

            var productIds = items
                .Where(x => x.ItemType == ItemType.Product &&
                            x.ProductId.HasValue &&
                            x.ProductId.Value != Guid.Empty)
                .Select(x => x.ProductId!.Value)
                .Distinct()
                .ToList();

            var result = new Dictionary<PriceItemKey, LatestItemPriceDto>();

            var materialDict = await LoadLatestMaterialPriceInfoDictAsync(
                materialIds.Select(x => (Guid?)x),
                cancellationToken);

            foreach (var kv in materialDict)
            {
                result[new PriceItemKey(ItemType.Material, kv.Key)] = new LatestItemPriceDto
                {
                    ItemType = ItemType.Material,
                    ItemId = kv.Key,
                    CurrentPrice = kv.Value.CurrentPrice,
                    PriceDate = kv.Value.PriceDate,
                    PriceSource = kv.Value.PriceSource switch
                    {
                        MaterialPriceSource.PurchaseOrder => LatestPriceSourceType.PurchaseOrder,
                        MaterialPriceSource.MaterialSupplier => LatestPriceSourceType.MaterialSupplier,
                        _ => LatestPriceSourceType.Unknown
                    }
                };
            }

            if (productIds.Count > 0)
            {
                // Giá giao dịch legacy: luồng tính Formula sẽ ưu tiên ProductPricingVersion Approved
                // qua LoadLatestPricingItemPriceInfoDictAsync, chỉ dùng giá này khi chưa có giá chuẩn.
                var latestProductRows = await _dbContext.MerchandiseOrderDetails
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .Where(x => productIds.Contains(x.ProductId))
                    .Where(x => x.MerchandiseOrder != null && x.MerchandiseOrder.IsActive)
                    .GroupBy(x => x.ProductId)
                    .Select(g => g
                        .OrderByDescending(x => x.MerchandiseOrder!.CreateDate)
                        .Select(x => new
                        {
                            x.ProductId,
                            Price = x.UnitPriceAgreed,
                            PriceDate = (DateTime?)x.MerchandiseOrder!.CreateDate
                        })
                        .FirstOrDefault()!)
                    .ToListAsync(cancellationToken);

                foreach (var x in latestProductRows)
                {
                    result[new PriceItemKey(ItemType.Product, x.ProductId)] = new LatestItemPriceDto
                    {
                        ItemType = ItemType.Product,
                        ItemId = x.ProductId,
                        CurrentPrice = x.Price,
                        PriceDate = x.PriceDate,
                        PriceSource = LatestPriceSourceType.MerchandiseOrder
                    };
                }

                foreach (var id in productIds)
                {
                    var key = new PriceItemKey(ItemType.Product, id);

                    if (!result.ContainsKey(key))
                    {
                        result[key] = new LatestItemPriceDto
                        {
                            ItemType = ItemType.Product,
                            ItemId = id,
                            CurrentPrice = 0m,
                            PriceDate = null,
                            PriceSource = LatestPriceSourceType.Unknown
                        };
                    }
                }
            }

            return result;
        }

        public async Task<Dictionary<PriceItemKey, LatestItemPriceDto>> LoadLatestPricingItemPriceInfoDictAsync(
            Guid companyId,
            string currency,
            IEnumerable<PriceItemRequest> items,
            CancellationToken cancellationToken = default)
        {
            var itemList = items
                .Select(x => new PriceItemRequest
                {
                    ItemType = x.ItemType is ItemType.Material or ItemType.MaterialFailure
                        ? ItemType.Material
                        : ItemType.Product,
                    MaterialId = x.MaterialId,
                    ProductId = x.ProductId
                })
                .ToArray();
            var result = await LoadLatestItemPriceInfoDictAsync(
                itemList.Where(x => x.ItemType is ItemType.Material or ItemType.MaterialFailure),
                cancellationToken);
            var productIds = itemList
                .Where(x => (x.ItemType is ItemType.Product or ItemType.ProductFailure) &&
                            x.ProductId.HasValue &&
                            x.ProductId.Value != Guid.Empty)
                .Select(x => x.ProductId!.Value)
                .Distinct()
                .ToArray();

            if (productIds.Length == 0)
            {
                return result;
            }

            var approvedProductIds = new HashSet<Guid>();
            if (companyId != Guid.Empty && !string.IsNullOrWhiteSpace(currency))
            {
                var normalizedCurrency = currency.Trim().ToUpperInvariant();
                var approvedPriceRows = await _dbContext.ProductPricingVersions
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == companyId &&
                        x.IsActive &&
                        x.Status == ProductPricingStatus.Approved &&
                        x.Currency == normalizedCurrency &&
                        productIds.Contains(x.ProductId) &&
                        x.StandardSellingPrice.HasValue)
                    .OrderByDescending(x => x.Version)
                    .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
                    .Select(x => new
                    {
                        x.ProductId,
                        UnitPrice = x.StandardSellingPrice!.Value,
                        PriceDate = x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate
                    })
                    .ToListAsync(cancellationToken);

                foreach (var price in approvedPriceRows
                             .GroupBy(x => x.ProductId)
                             .Select(x => x.First()))
                {
                    approvedProductIds.Add(price.ProductId);
                    result[new PriceItemKey(ItemType.Product, price.ProductId)] = new LatestItemPriceDto
                    {
                        ItemType = ItemType.Product,
                        ItemId = price.ProductId,
                        CurrentPrice = price.UnitPrice,
                        PriceDate = price.PriceDate,
                        PriceSource = LatestPriceSourceType.ProductPricingVersion
                    };
                }
            }

            var legacyProductIds = productIds
                .Where(x => !approvedProductIds.Contains(x))
                .ToArray();

            var formulaCostByProduct = await LoadSelectedFormulaMaterialCostsAsync(
                companyId,
                currency,
                legacyProductIds,
                cancellationToken);

            foreach (var (productId, formulaCost) in formulaCostByProduct)
            {
                result[new PriceItemKey(ItemType.Product, productId)] = new LatestItemPriceDto
                {
                    ItemType = ItemType.Product,
                    ItemId = productId,
                    CurrentPrice = formulaCost.Cost,
                    PriceDate = formulaCost.PriceDate,
                    PriceSource = LatestPriceSourceType.FormulaMaterialCost
                };
            }

            if (legacyProductIds.Length > 0)
            {
                var unresolvedProductIds = legacyProductIds
                    .Where(x => !formulaCostByProduct.ContainsKey(x))
                    .ToArray();

                // Legacy fallback: chỉ chạy khi TP chưa có giá chuẩn và không tính được giá NVL từ Formula đang áp dụng.
                var latestProductRows = await _dbContext.MerchandiseOrderDetails
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .Where(x => unresolvedProductIds.Contains(x.ProductId))
                    .Where(x => x.MerchandiseOrder != null && x.MerchandiseOrder.IsActive)
                    .GroupBy(x => x.ProductId)
                    .Select(g => g
                        .OrderByDescending(x => x.MerchandiseOrder!.CreateDate)
                        .Select(x => new
                        {
                            x.ProductId,
                            Price = x.UnitPriceAgreed,
                            PriceDate = (DateTime?)x.MerchandiseOrder!.CreateDate
                        })
                        .FirstOrDefault()!)
                    .ToListAsync(cancellationToken);

                foreach (var price in latestProductRows)
                {
                    result[new PriceItemKey(ItemType.Product, price.ProductId)] = new LatestItemPriceDto
                    {
                        ItemType = ItemType.Product,
                        ItemId = price.ProductId,
                        CurrentPrice = price.Price,
                        PriceDate = price.PriceDate,
                        PriceSource = LatestPriceSourceType.MerchandiseOrder
                    };
                }
            }

            foreach (var productId in legacyProductIds)
            {
                var key = new PriceItemKey(ItemType.Product, productId);
                if (!result.ContainsKey(key))
                {
                    result[key] = new LatestItemPriceDto
                    {
                        ItemType = ItemType.Product,
                        ItemId = productId,
                        CurrentPrice = 0m,
                        PriceDate = null,
                        PriceSource = LatestPriceSourceType.Unknown
                    };
                }
            }

            return result;
        }

        /// <summary>
        /// Tính giá vốn TP theo Formula đang được chọn (IsSelect). Chỉ cộng giá của các dòng NVL;
        /// TP lồng nhau được resolve đệ quy theo cùng thứ tự ưu tiên và có chặn vòng lặp.
        /// Toàn bộ Formula, dòng Formula, giá chuẩn và giá NVL được tải theo batch, không query từng dòng.
        /// </summary>
        private async Task<Dictionary<Guid, FormulaMaterialCost>> LoadSelectedFormulaMaterialCostsAsync(
            Guid companyId,
            string currency,
            IReadOnlyCollection<Guid> rootProductIds,
            CancellationToken cancellationToken)
        {
            if (companyId == Guid.Empty ||
                string.IsNullOrWhiteSpace(currency) ||
                rootProductIds.Count == 0)
            {
                return new Dictionary<Guid, FormulaMaterialCost>();
            }

            var normalizedCurrency = currency.Trim().ToUpperInvariant();
            var discoveredProductIds = new HashSet<Guid>(rootProductIds);
            var queriedProductIds = new HashSet<Guid>();
            var selectedFormulaByProduct = new Dictionary<Guid, HRM.Domain.Entities.SampleRequestSchema.Formula>();
            var materialLinesByFormula = new Dictionary<Guid, List<HRM.Domain.Entities.SampleRequestSchema.FormulaMaterial>>();

            while (true)
            {
                var pendingProductIds = discoveredProductIds
                    .Where(x => queriedProductIds.Add(x))
                    .ToArray();
                if (pendingProductIds.Length == 0)
                {
                    break;
                }

                var selectedFormulaRows = await _dbContext.Formulas
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == companyId &&
                        x.IsActive &&
                        x.IsSelect &&
                        pendingProductIds.Contains(x.ProductId))
                    .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                    .ThenByDescending(x => x.FormulaId)
                    .ToListAsync(cancellationToken);

                foreach (var formula in selectedFormulaRows)
                {
                    selectedFormulaByProduct.TryAdd(formula.ProductId, formula);
                }

                var formulaIds = selectedFormulaRows
                    .Select(x => x.FormulaId)
                    .Distinct()
                    .ToArray();
                if (formulaIds.Length == 0)
                {
                    continue;
                }

                var materialLines = await _dbContext.FormulaMaterials
                    .AsNoTracking()
                    .Where(x => x.IsActive && formulaIds.Contains(x.FormulaId))
                    .OrderBy(x => x.LineNo)
                    .ToListAsync(cancellationToken);

                foreach (var group in materialLines.GroupBy(x => x.FormulaId))
                {
                    materialLinesByFormula[group.Key] = group.ToList();
                }

                foreach (var productId in materialLines
                             .Where(x => x.itemType is ItemType.Product or ItemType.ProductFailure)
                             .Select(x => x.ProductId)
                             .Where(x => x.HasValue && x.Value != Guid.Empty)
                             .Select(x => x!.Value))
                {
                    discoveredProductIds.Add(productId);
                }
            }

            if (selectedFormulaByProduct.Count == 0)
            {
                return new Dictionary<Guid, FormulaMaterialCost>();
            }

            var standardSellingPriceRows = await _dbContext.ProductPricingVersions
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    x.Status == ProductPricingStatus.Approved &&
                    x.Currency == normalizedCurrency &&
                    discoveredProductIds.Contains(x.ProductId) &&
                    x.StandardSellingPrice.HasValue)
                .OrderByDescending(x => x.Version)
                .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
                .Select(x => new { x.ProductId, Price = x.StandardSellingPrice!.Value })
                .ToListAsync(cancellationToken);
            var approvedPriceByProduct = standardSellingPriceRows
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.First().Price);

            var materialIds = materialLinesByFormula.Values
                .SelectMany(x => x)
                .Where(x => x.itemType is ItemType.Material or ItemType.MaterialFailure)
                .Select(x => x.MaterialId)
                .Where(x => x.HasValue && x.Value != Guid.Empty)
                .Select(x => x!.Value)
                .Distinct()
                .ToArray();
            var latestMaterialPriceById = await LoadLatestMaterialPriceInfoDictAsync(
                materialIds.Select(x => (Guid?)x), cancellationToken);
            var calculatedCostByProduct = new Dictionary<Guid, FormulaMaterialCost?>();
            var resolvingProductIds = new HashSet<Guid>();

            FormulaMaterialCost? ResolveProductCost(Guid productId)
            {
                if (approvedPriceByProduct.TryGetValue(productId, out var approvedPrice))
                {
                    return new FormulaMaterialCost(approvedPrice, null);
                }

                if (calculatedCostByProduct.TryGetValue(productId, out var cachedCost))
                {
                    return cachedCost;
                }

                if (!resolvingProductIds.Add(productId) ||
                    !selectedFormulaByProduct.TryGetValue(productId, out var formula) ||
                    !materialLinesByFormula.TryGetValue(formula.FormulaId, out var lines) ||
                    lines.Count == 0)
                {
                    return null;
                }

                decimal totalCost = 0m;
                foreach (var line in lines)
                {
                    decimal? unitPrice = line.itemType switch
                    {
                        ItemType.Material or ItemType.MaterialFailure when line.MaterialId.HasValue &&
                            latestMaterialPriceById.TryGetValue(line.MaterialId.Value, out var materialPrice) &&
                            materialPrice.PriceSource != MaterialPriceSource.Unknown
                            => materialPrice.CurrentPrice,
                        ItemType.Product or ItemType.ProductFailure when line.ProductId.HasValue
                            => ResolveProductCost(line.ProductId.Value)?.Cost,
                        _ => null
                    };

                    if (!unitPrice.HasValue)
                    {
                        resolvingProductIds.Remove(productId);
                        calculatedCostByProduct[productId] = null;
                        return null;
                    }

                    totalCost += line.Quantity * unitPrice.Value;
                }

                resolvingProductIds.Remove(productId);
                var formulaCost = new FormulaMaterialCost(
                    PricingRoundingRules.RoundStoredInput(totalCost),
                    formula.UpdatedDate ?? formula.CreatedDate);
                calculatedCostByProduct[productId] = formulaCost;
                return formulaCost;
            }

            var result = new Dictionary<Guid, FormulaMaterialCost>();
            foreach (var productId in rootProductIds)
            {
                var cost = ResolveProductCost(productId);
                if (cost.HasValue)
                {
                    result[productId] = cost.Value;
                }
            }

            return result;
        }

        private readonly record struct FormulaMaterialCost(decimal Cost, DateTime? PriceDate);

        private static List<Guid> NormalizeIds(IEnumerable<Guid?> materialIds)
        {
            return materialIds
                .Where(x => x.HasValue && x.Value != Guid.Empty)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
        }
    }
}
