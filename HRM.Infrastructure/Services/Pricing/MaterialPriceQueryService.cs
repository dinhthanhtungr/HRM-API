using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.Commons.Pricing;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Pricing.Rules;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Products;
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

        /// <summary>
        /// Tải giá hiện hành của danh sách nguyên vật liệu.
        /// Giá được xác định từ đơn mua hàng, nhà cung cấp hoặc quy tắc tính giá nội bộ.
        /// </summary>
        /// <param name="materialIds">Danh sách ID nguyên vật liệu.</param>
        /// <param name="cancellationToken">Token dùng để hủy tác vụ bất đồng bộ.</param>
        /// <returns>
        /// Dictionary có key là MaterialId và value là thông tin giá hiện hành.
        /// </returns>
        public async Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestMaterialPriceInfoDictAsync(
            IEnumerable<Guid?> materialIds,
            CancellationToken cancellationToken = default)
        {
            var ids = NormalizeIds(materialIds);
            var directPrices = await LoadLatestDirectMaterialPriceInfoDictAsync(
                ids.Select(x => (Guid?)x), cancellationToken);
            if (ids.Count == 0)
            {
                return directPrices;
            }

            var requestedMaterials = await _dbContext.Materials
                .AsNoTracking()
                .Where(x => ids.Contains(x.MaterialId))
                .Select(x => new MaterialCostingMaterial(x.MaterialId, x.CompanyId, x.Name, x.IsActive ?? false))
                .ToListAsync(cancellationToken);
            foreach (var material in requestedMaterials)
            {
                if (directPrices.TryGetValue(material.MaterialId, out var directPrice) &&
                    directPrice.PriceSource != MaterialPriceSource.Unknown)
                {
                    directPrice.Calculation = CreateDirectPriceCalculation(
                        material.MaterialId,
                        material.Name,
                        directPrice.CurrentPrice,
                        ToLatestPriceSourceType(directPrice.PriceSource));
                }
            }
            var derivedMaterials = requestedMaterials
                .Select(x => new
                {
                    Material = x,
                    IsDerived = false,
                    Rule = InternalMaterialCostingRule.None,
                    SourceNameKey = string.Empty
                })
                .Where(x => x.IsDerived)
                .ToArray();
            if (derivedMaterials.Length == 0)
            {
                return directPrices;
            }

            var companyIds = derivedMaterials.Select(x => x.Material.CompanyId).Distinct().ToArray();
            var sourceCandidates = await _dbContext.Materials
                .AsNoTracking()
                .Where(x => (x.IsActive ?? true) && companyIds.Contains(x.CompanyId))
                .Select(x => new MaterialCostingMaterial(x.MaterialId, x.CompanyId, x.Name, x.IsActive ?? false))
                .ToListAsync(cancellationToken);
            var baseMaterials = sourceCandidates
                .Where(x => !InternalMaterialCostingRules.TryResolveMaterialRule(x.Name, out _, out _))
                .ToArray();
            var basePrices = await LoadLatestDirectMaterialPriceInfoDictAsync(
                baseMaterials.Select(x => (Guid?)x.MaterialId), cancellationToken);

            foreach (var derived in derivedMaterials)
            {
                var matchedSources = baseMaterials
                    .Where(x => x.CompanyId == derived.Material.CompanyId &&
                                string.Equals(
                                    InternalMaterialCostingRules.NormalizeComparableName(x.Name),
                                    derived.SourceNameKey,
                                    StringComparison.Ordinal))
                    .ToArray();

                if (matchedSources.Length != 1 ||
                    !basePrices.TryGetValue(matchedSources[0].MaterialId, out var sourcePrice) ||
                    sourcePrice.PriceSource == MaterialPriceSource.Unknown)
                {
                    directPrices[derived.Material.MaterialId] = new LatestMaterialPriceDto
                    {
                    MaterialId = derived.Material.MaterialId,
                    CurrentPrice = 0m,
                    PriceDate = null,
                    PriceSource = MaterialPriceSource.Unknown,
                    Calculation = new PriceCalculationDetailDto
                    {
                        RuleCode = "UNRESOLVED_INTERNAL_RULE",
                        DisplayText = "Không xác định được duy nhất nguyên liệu gốc hoặc giá gốc.",
                        CalculatedUnitPrice = 0m,
                        IsComplete = false
                    }
                    };
                    continue;
                }

                directPrices[derived.Material.MaterialId] = new LatestMaterialPriceDto
                {
                    MaterialId = derived.Material.MaterialId,
                    CurrentPrice = PricingRoundingRules.RoundStoredInput(
                        InternalMaterialCostingRules.ApplyMaterialRule(derived.Rule, sourcePrice.CurrentPrice)),
                    PriceDate = sourcePrice.PriceDate,
                    PriceSource = MaterialPriceSource.InternalCostRule,
                    Calculation = CreateMaterialRuleCalculation(
                        derived.Rule,
                        matchedSources[0].MaterialId,
                        matchedSources[0].Name,
                        sourcePrice.CurrentPrice,
                        ToLatestPriceSourceType(sourcePrice.PriceSource))
                };
            }

            return directPrices;
        }

        /// <summary>
        /// Tải giá trực tiếp mới nhất của nguyên vật liệu từ đơn mua hàng
        /// và bảng giá theo nhà cung cấp, chưa áp dụng quy tắc tính giá nội bộ.
        /// </summary>
        /// <param name="materialIds">Danh sách ID nguyên vật liệu.</param>
        /// <param name="cancellationToken">Token dùng để hủy tác vụ bất đồng bộ.</param>
        /// <returns>
        /// Dictionary chứa giá trực tiếp được chọn cho từng nguyên vật liệu.
        /// </returns>
        private async Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestDirectMaterialPriceInfoDictAsync(
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
                    .ThenByDescending(x => x.PurchaseOrderDetailId)
                    .ThenByDescending(x => x.UnitPriceAgreed.HasValue)
                    .ThenByDescending(x => x.UnitPriceAgreed)
                    .Select(x => new
                    {
                        x.PurchaseOrderDetailId,
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
                    CandidateId = x.PurchaseOrderDetailId,
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
                    .ThenByDescending(x => x.MaterialsSuppliersId)
                    .ThenByDescending(x => x.CurrentPrice.HasValue)
                    .ThenByDescending(x => x.CurrentPrice)
                    .Select(x => new
                    {
                        x.MaterialsSuppliersId,
                        x.MaterialId,
                        x.CurrentPrice,
                        PriceDate = x.UpdatedDate ?? x.CreateDate,
                        IsPreferred = x.IsPreferred ?? false
                    })
                    .FirstOrDefault()!)
                .ToListAsync(cancellationToken);

            var supplierDict = supplierRows.ToDictionary(
                x => x.MaterialId,
                x => new MaterialPriceCandidate
                {
                    MaterialId = x.MaterialId,
                    CandidateId = x.MaterialsSuppliersId,
                    CurrentPrice = x.CurrentPrice ?? 0m,
                    HasPriceValue = x.CurrentPrice.HasValue,
                    PriceDate = x.PriceDate,
                    PriceSource = MaterialPriceSource.MaterialSupplier,
                    IsPreferred = x.IsPreferred
                });

            return MaterialLatestPriceSelector.SelectMany(ids, poDict, supplierDict);
        }

        /// <summary>
        /// Tải giá mới nhất của các nguyên vật liệu theo một nhà cung cấp cụ thể.
        /// Giá được lấy từ đơn mua hàng của nhà cung cấp hoặc giá khai báo trong bảng nhà cung cấp.
        /// </summary>
        /// <param name="supplierId">ID nhà cung cấp.</param>
        /// <param name="materialIds">Danh sách ID nguyên vật liệu.</param>
        /// <param name="cancellationToken">Token dùng để hủy tác vụ bất đồng bộ.</param>
        /// <returns>
        /// Dictionary chứa giá hiện hành của từng nguyên vật liệu theo nhà cung cấp.
        /// </returns>
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
                    .ThenByDescending(x => x.PurchaseOrderDetailId)
                    .ThenByDescending(x => x.UnitPriceAgreed.HasValue)
                    .ThenByDescending(x => x.UnitPriceAgreed)
                    .Select(x => new
                    {
                        x.PurchaseOrderDetailId,
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
                    CandidateId = x.PurchaseOrderDetailId,
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
                    .ThenByDescending(x => x.MaterialsSuppliersId)
                    .ThenByDescending(x => x.CurrentPrice.HasValue)
                    .ThenByDescending(x => x.CurrentPrice)
                    .Select(x => new
                    {
                        x.MaterialsSuppliersId,
                        x.MaterialId,
                        x.CurrentPrice,
                        PriceDate = x.UpdatedDate ?? x.CreateDate,
                        IsPreferred = x.IsPreferred ?? false
                    })
                    .FirstOrDefault()!)
                .ToListAsync(cancellationToken);

            var supplierDict = supplierRows.ToDictionary(
                x => x.MaterialId,
                x => new MaterialPriceCandidate
                {
                    MaterialId = x.MaterialId,
                    CandidateId = x.MaterialsSuppliersId,
                    CurrentPrice = x.CurrentPrice ?? 0m,
                    HasPriceValue = x.CurrentPrice.HasValue,
                    PriceDate = x.PriceDate,
                    PriceSource = MaterialPriceSource.MaterialSupplier,
                    IsPreferred = x.IsPreferred
                });

            return MaterialLatestPriceSelector.SelectMany(ids, poDict, supplierDict);
        }

        /// <summary>
        /// Tải giá hiện hành của danh sách item gồm nguyên vật liệu và thành phẩm.
        /// Giá thành phẩm trong hàm này được lấy từ giao dịch bán hàng gần nhất.
        /// </summary>
        /// <param name="items">Danh sách item cần lấy giá.</param>
        /// <param name="cancellationToken">Token dùng để hủy tác vụ bất đồng bộ.</param>
        /// <returns>
        /// Dictionary có key gồm loại item và ID item, value là thông tin giá hiện hành.
        /// </returns>
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
                    Calculation = kv.Value.Calculation,
                    PriceSource = kv.Value.PriceSource switch
                    {
                        MaterialPriceSource.PurchaseOrder => LatestPriceSourceType.PurchaseOrder,
                        MaterialPriceSource.MaterialSupplier => LatestPriceSourceType.MaterialSupplier,
                        MaterialPriceSource.InternalCostRule => LatestPriceSourceType.InternalCostRule,
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

        /// <summary>
        /// Tải giá phục vụ tính Pricing cho nguyên vật liệu và thành phẩm.
        /// Giá thành phẩm được ưu tiên theo giá chuẩn Approved, giá vốn Formula,
        /// sau đó mới sử dụng giá giao dịch bán hàng gần nhất.
        /// </summary>
        /// <param name="companyId">ID công ty sở hữu dữ liệu.</param>
        /// <param name="currency">Mã tiền tệ dùng để tìm phiên bản giá chuẩn.</param>
        /// <param name="items">Danh sách item cần lấy giá.</param>
        /// <param name="cancellationToken">Token dùng để hủy tác vụ bất đồng bộ.</param>
        /// <returns>
        /// Dictionary chứa giá và nguồn giá hiện hành của từng item.
        /// </returns>
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

            var internalProductAdjustments = await LoadInternalProductCostAdjustmentsAsync(
                companyId, productIds, cancellationToken);
            var internalProductIds = internalProductAdjustments.Keys.ToHashSet();
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
                        !internalProductIds.Contains(x.ProductId) &&
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
                        Calculation = formulaCost.Calculation,
                        PriceSource = formulaCost.IsInternalCostRule
                            ? LatestPriceSourceType.InternalCostRule
                            : LatestPriceSourceType.FormulaMaterialCost
                };
            }

            if (legacyProductIds.Length > 0)
            {
                var unresolvedProductIds = legacyProductIds
                    .Where(x => !internalProductIds.Contains(x) && !formulaCostByProduct.ContainsKey(x))
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
        /// Tính giá vốn TP theo Formula đang được chọn (IsSelect), rồi Formula SampleSent mới nhất.
        /// Chỉ cộng giá của các dòng NVL; TP lồng nhau được resolve đệ quy theo cùng thứ tự ưu tiên và có chặn vòng lặp.
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
            var formulaCandidatesByProduct =
                new Dictionary<Guid, IReadOnlyList<HRM.Domain.Entities.SampleRequestSchema.Formula>>();
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

                var formulaCandidateRows = await _dbContext.Formulas
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == companyId &&
                        x.IsActive &&
                        (x.IsSelect || x.Status == FormulaStatus.SampleSent.ToString()) &&
                        pendingProductIds.Contains(x.ProductId))
                    .OrderByDescending(x => x.IsSelect)
                    .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                    .ThenByDescending(x => x.FormulaId)
                    .ToListAsync(cancellationToken);

                foreach (var group in formulaCandidateRows.GroupBy(x => x.ProductId))
                {
                    formulaCandidatesByProduct.TryAdd(group.Key, group.ToArray());
                }

                var formulaIds = formulaCandidateRows
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

            if (formulaCandidatesByProduct.Count == 0)
            {
                return new Dictionary<Guid, FormulaMaterialCost>();
            }

            var internalProductAdjustments = await LoadInternalProductCostAdjustmentsAsync(
                companyId, discoveredProductIds, cancellationToken);
            var internalProductIds = internalProductAdjustments.Keys.ToHashSet();
            var standardSellingPriceRows = await _dbContext.ProductPricingVersions
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    x.Status == ProductPricingStatus.Approved &&
                    x.Currency == normalizedCurrency &&
                    discoveredProductIds.Contains(x.ProductId) &&
                    !internalProductIds.Contains(x.ProductId) &&
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
                    !formulaCandidatesByProduct.TryGetValue(productId, out var formulaCandidates))
                {
                    return null;
                }

                foreach (var formula in formulaCandidates)
                {
                    if (!materialLinesByFormula.TryGetValue(formula.FormulaId, out var lines) ||
                        lines.Count == 0)
                    {
                        continue;
                    }

                    decimal totalCost = 0m;
                    var canCalculate = true;
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
                            canCalculate = false;
                            break;
                        }

                        totalCost += line.Quantity * unitPrice.Value;
                    }

                    if (!canCalculate)
                    {
                        continue;
                    }

                    var adjustment = internalProductAdjustments.GetValueOrDefault(productId);
                    var adjustedCost = PricingRoundingRules.RoundStoredInput(
                        InternalMaterialCostingRules.ApplyProductCostAdjustment(totalCost, adjustment));
                    var formulaCost = new FormulaMaterialCost(
                        adjustedCost,
                        formula.UpdatedDate ?? formula.CreatedDate,
                        adjustment.IsInternalCostRule,
                        adjustment.IsInternalCostRule
                            ? CreateProductRuleCalculation(totalCost, adjustedCost, adjustment)
                            : null);
                    resolvingProductIds.Remove(productId);
                    calculatedCostByProduct[productId] = formulaCost;
                    return formulaCost;
                }

                resolvingProductIds.Remove(productId);
                calculatedCostByProduct[productId] = null;
                return null;
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

        /// <summary>
        /// Tải các quy tắc điều chỉnh giá vốn nội bộ áp dụng cho danh sách thành phẩm.
        /// Quy tắc được xác định dựa trên tên thành phẩm và mã danh mục.
        /// </summary>
        /// <param name="companyId">ID công ty sở hữu sản phẩm.</param>
        /// <param name="productIds">Danh sách ID thành phẩm.</param>
        /// <param name="cancellationToken">Token dùng để hủy tác vụ bất đồng bộ.</param>
        /// <returns>
        /// Dictionary chứa quy tắc điều chỉnh giá vốn theo ProductId.
        /// </returns>  
        private async Task<Dictionary<Guid, InternalProductCostAdjustment>> LoadInternalProductCostAdjustmentsAsync(
            Guid companyId,
            IEnumerable<Guid> productIds,
            CancellationToken cancellationToken)
        {
            var ids = productIds.Where(x => x != Guid.Empty).Distinct().ToArray();
            if (companyId == Guid.Empty || ids.Length == 0)
            {
                return new Dictionary<Guid, InternalProductCostAdjustment>();
            }

            var products = await _dbContext.Products
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.IsActive && ids.Contains(x.ProductId))
                .Select(x => new { x.ProductId, x.Name, CategoryExternalId = x.Category!.ExternalId })
                .ToListAsync(cancellationToken);
            return products
                .Select(x => new
                {
                    x.ProductId,
                    Adjustment = InternalMaterialCostingRules.ResolveProductCostAdjustment(
                        x.Name, x.CategoryExternalId)
                })
                .Where(x => x.Adjustment.IsInternalCostRule)
                .ToDictionary(x => x.ProductId, x => x.Adjustment);
        }

        private readonly record struct FormulaMaterialCost(
            decimal Cost,
            DateTime? PriceDate,
            bool IsInternalCostRule = false,
            PriceCalculationDetailDto? Calculation = null);

        private readonly record struct MaterialCostingMaterial(
            Guid MaterialId,
            Guid CompanyId,
            string? Name,
            bool IsActive);

        /// <summary>
        /// Tạo thông tin giải thích cho giá nguyên vật liệu được lấy trực tiếp.
        /// </summary>
        private static PriceCalculationDetailDto CreateDirectPriceCalculation(
            Guid itemId,
            string? itemName,
            decimal unitPrice,
            LatestPriceSourceType source) => new()
        {
            RuleCode = "DIRECT_PRICE",
            DisplayText = $"Giá NVL hiện hành: {unitPrice:N0} đ/kg.",
            BaseItemId = itemId,
            BaseItemName = itemName,
            BaseUnitPrice = unitPrice,
            BasePriceSource = source,
            CalculatedUnitPrice = unitPrice,
            IsComplete = true
        };

        /// <summary>
        /// Tạo thông tin giải thích phép tính giá nguyên vật liệu theo quy tắc nội bộ.
        /// </summary>
        private static PriceCalculationDetailDto CreateMaterialRuleCalculation(
            InternalMaterialCostingRule rule,
            Guid baseItemId,
            string? baseItemName,
            decimal baseUnitPrice,
            LatestPriceSourceType basePriceSource)
        {
            var calculatedUnitPrice = PricingRoundingRules.RoundStoredInput(
                InternalMaterialCostingRules.ApplyMaterialRule(rule, baseUnitPrice));
            return rule switch
            {
                InternalMaterialCostingRule.GroundResin => new PriceCalculationDetailDto
                {
                    RuleCode = "GROUND_RESIN",
                    DisplayText = $"Giá {baseItemName} {baseUnitPrice:N0} đ/kg + 5.000 đ/kg = {calculatedUnitPrice:N0} đ/kg.",
                    BaseItemId = baseItemId,
                    BaseItemName = baseItemName,
                    BaseUnitPrice = baseUnitPrice,
                    BasePriceSource = basePriceSource,
                    FixedCostPerKg = InternalMaterialCostingRules.GrindingCostPerKg,
                    CalculatedUnitPrice = calculatedUnitPrice,
                    IsComplete = true
                },
                InternalMaterialCostingRule.DilutedPigment => new PriceCalculationDetailDto
                {
                    RuleCode = "DILUTED_PIGMENT",
                    DisplayText = $"Giá {baseItemName} {baseUnitPrice:N0} đ/kg x 70% = {calculatedUnitPrice:N0} đ/kg.",
                    BaseItemId = baseItemId,
                    BaseItemName = baseItemName,
                    BaseUnitPrice = baseUnitPrice,
                    BasePriceSource = basePriceSource,
                    Rate = InternalMaterialCostingRules.DilutedPigmentRate,
                    CalculatedUnitPrice = calculatedUnitPrice,
                    IsComplete = true
                },
                _ => throw new ArgumentOutOfRangeException(nameof(rule))
            };
        }

        /// <summary>
        /// Chuyển đổi nguồn giá nguyên vật liệu sang loại nguồn giá dùng chung.
        /// </summary>
        private static LatestPriceSourceType ToLatestPriceSourceType(MaterialPriceSource source) => source switch
        {
            MaterialPriceSource.PurchaseOrder => LatestPriceSourceType.PurchaseOrder,
            MaterialPriceSource.MaterialSupplier => LatestPriceSourceType.MaterialSupplier,
            MaterialPriceSource.InternalCostRule => LatestPriceSourceType.InternalCostRule,
            _ => LatestPriceSourceType.Unknown
        };

        /// <summary>
        /// Tạo thông tin giải thích phép điều chỉnh giá vốn thành phẩm theo quy tắc nội bộ.
        /// </summary>
        private static PriceCalculationDetailDto CreateProductRuleCalculation(
            decimal formulaMaterialCost,
            decimal calculatedUnitPrice,
            InternalProductCostAdjustment adjustment)
        {
            if (adjustment.MaterialRule == InternalMaterialCostingRule.GroundResin)
            {
                return new PriceCalculationDetailDto
                {
                    RuleCode = "GROUND_RESIN",
                    DisplayText = $" {formulaMaterialCost:N0} đ/kg + 5.000 đ/kg = {calculatedUnitPrice:N0} đ/kg.",
                    FormulaMaterialCost = formulaMaterialCost,
                    FixedCostPerKg = InternalMaterialCostingRules.GrindingCostPerKg,
                    CalculatedUnitPrice = calculatedUnitPrice,
                    IsComplete = true
                };
            }

            if (adjustment.MaterialRule == InternalMaterialCostingRule.DilutedPigment)
            {
                return new PriceCalculationDetailDto
                {
                    RuleCode = "DILUTED_PIGMENT",
                    DisplayText = $" {formulaMaterialCost:N0} đ/kg x 70% = {calculatedUnitPrice:N0} đ/kg.",
                    FormulaMaterialCost = formulaMaterialCost,
                    Rate = InternalMaterialCostingRules.DilutedPigmentRate,
                    CalculatedUnitPrice = calculatedUnitPrice,
                    IsComplete = true
                };
            }

            var isColorMasterbatch = adjustment.SurchargePerKg == InternalMaterialCostingRules.ColorMasterbatchCostPerKg;
            return new PriceCalculationDetailDto
            {
                RuleCode = isColorMasterbatch ? "COLOR_MASTERBATCH" : "COMPOUND",
                DisplayText = $" {formulaMaterialCost:N0} đ/kg + {adjustment.SurchargePerKg:N0} đ/kg = {calculatedUnitPrice:N0} đ/kg.",
                FormulaMaterialCost = formulaMaterialCost,
                FixedCostPerKg = adjustment.SurchargePerKg,
                CalculatedUnitPrice = calculatedUnitPrice,
                IsComplete = true
            };
        }

        /// <summary>
        /// Chuẩn hóa danh sách ID bằng cách loại bỏ giá trị null, Guid rỗng và ID trùng lặp.
        /// </summary>
        /// <param name="materialIds">Danh sách ID cần chuẩn hóa.</param>
        /// <returns>Danh sách ID hợp lệ và không trùng lặp.</returns>
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
