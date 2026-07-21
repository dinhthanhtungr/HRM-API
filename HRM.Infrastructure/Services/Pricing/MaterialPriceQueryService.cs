using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.Commons.Pricing;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.Services.Pricing
{
    public class MaterialPriceQueryService : IMaterialPriceQueryService
    {
        private static readonly string[] CanceledPurchaseOrderStatuses = ["Canceled", "Cancelled"];

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
                     !CanceledPurchaseOrderStatuses.Contains(x.PurchaseOrder.Status)))
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
                        CurrentPrice = x.CurrentPrice ?? 0m,
                        PriceDate = x.UpdatedDate ?? x.CreateDate
                    })
                    .FirstOrDefault()!)
                .ToListAsync(cancellationToken);

            var supplierDict = supplierRows.ToDictionary(
                x => x.MaterialId,
                x => new MaterialPriceCandidate
                {
                    MaterialId = x.MaterialId,
                    CurrentPrice = x.CurrentPrice,
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
                     !CanceledPurchaseOrderStatuses.Contains(x.PurchaseOrder.Status)))
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
                        CurrentPrice = x.CurrentPrice ?? 0m,
                        PriceDate = x.UpdatedDate ?? x.CreateDate
                    })
                    .FirstOrDefault()!)
                .ToListAsync(cancellationToken);

            var supplierDict = supplierRows.ToDictionary(
                x => x.MaterialId,
                x => new MaterialPriceCandidate
                {
                    MaterialId = x.MaterialId,
                    CurrentPrice = x.CurrentPrice,
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
