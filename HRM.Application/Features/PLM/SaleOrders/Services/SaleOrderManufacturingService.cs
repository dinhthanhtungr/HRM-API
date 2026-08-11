using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

/// <summary>
/// Tạo MFG theo SaleOrder bằng dữ liệu product và công thức hiện hành trong database.
/// Context VA/VU, vật tư và giá được tải theo batch để giữ cùng nền dữ liệu với flow MFG cũ.
/// </summary>
internal sealed class SaleOrderManufacturingService
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly IExternalIdService _externalIdService;
    private readonly IEventLogWriter _eventLogWriter;

    public SaleOrderManufacturingService(
        ISaleOrderDbContext dbContext,
        IExternalIdService externalIdService,
        IEventLogWriter eventLogWriter)
    {
        _dbContext = dbContext;
        _externalIdService = externalIdService;
        _eventLogWriter = eventLogWriter;
    }

    /// <summary>
    /// Tạo một MFG và liên kết MFG-PO cho từng dòng SaleOrder, đồng thời ghi timeline có parent linkage.
    /// Trả số MFG đã tạo để caller kiểm tra đủ dòng trước khi commit transaction.
    /// </summary>
    public async Task<OperationResult<int>> CreateFromSaleOrderAsync(
        MerchandiseOrder order,
        IReadOnlyCollection<MerchandiseOrderDetail> details,
        Guid employeeId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (details.Count == 0)
        {
            return OperationResult<int>.Fail("Đơn hàng không có chi tiết hợp lệ để tạo lệnh sản xuất.");
        }

        var contextResult = await BuildContextAsync(
            order.CompanyId,
            details,
            now,
            cancellationToken);
        if (!contextResult.Success || contextResult.Data is null)
        {
            return OperationResult<int>.Fail(
                contextResult.Message ?? "Không thể tải dữ liệu nền để tạo lệnh sản xuất.");
        }

        var context = contextResult.Data;
        var createdCount = 0;
        foreach (var detail in details)
        {
            if (!context.Products.TryGetValue(detail.ProductId, out var product))
            {
                return OperationResult<int>.Fail($"Không tìm thấy sản phẩm {detail.ProductId}.");
            }

            // MFG hiện vẫn lưu FormulaId VU từ detail như backend cũ. Context này xác định nguồn
            // vật tư VA chuẩn hoặc VU fallback để mọi bundle trong batch dùng cùng dữ liệu nền.
            if (!context.TryResolveFormula(detail, out _))
            {
                return OperationResult<int>.Fail(
                    $"Không tìm thấy công thức hợp lệ cho dòng {detail.MerchandiseOrderDetailId}.");
            }

            var mfg = new MfgProductionOrder
            {
                MfgProductionOrderId = Guid.CreateVersion7(),
                ExternalId = await _externalIdService.GenerateMonthlyCodeAsync(
                    order.CompanyId,
                    DocumentPrefix.MFG.ToString(),
                    cancellationToken),
                ProductId = product.ProductId,
                ProductExternalIdSnapshot = product.ColourCode,
                ProductNameSnapshot = product.Name,
                ColorName = product.ColourName,
                CustomerId = order.CustomerId,
                CustomerNameSnapshot = order.CustomerNameSnapshot,
                CustomerExternalIdSnapshot = order.CustomerExternalIdSnapshot,
                FormulaId = detail.FormulaId,
                FormulaExternalIdSnapshot = detail.FormulaExternalIdSnapshot,
                ManufacturingDate = null,
                ExpectedDate = null,
                RequiredDate = detail.DeliveryRequestDate == default ? now : detail.DeliveryRequestDate,
                TotalQuantityRequest = detail.ExpectedQuantity,
                TotalQuantity = null,
                NumOfBatches = null,
                UnitPriceAgreed = detail.UnitPriceAgreed,
                Status = ManufacturingProductOrder.New.ToString(),
                LabNote = null,
                Requirement = detail.Comment,
                PlpuNote = null,
                BagType = detail.BagType,
                IsActive = true,
                QcCheck = null,
                StepOfProduct = null,
                CompanyId = order.CompanyId,
                CreatedDate = now,
                CreatedBy = employeeId,
                UpdatedDate = now,
                UpdatedBy = employeeId
            };

            await _dbContext.MfgProductionOrders.AddAsync(mfg, cancellationToken);
            await _dbContext.MfgOrderPOs.AddAsync(new MfgOrderPO
            {
                MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId,
                MfgProductionOrderId = mfg.MfgProductionOrderId,
                IsActive = true
            }, cancellationToken);

            await _eventLogWriter.AddAsync(new EventLogCreateRequest
            {
                EmployeeId = employeeId,
                CompanyId = order.CompanyId,
                SourceType = "MfgProductionOrder",
                ParentSourceType = "MerchandiseOrder",
                ParentSourceId = order.MerchandiseOrderId,
                SourceId = mfg.MfgProductionOrderId,
                SourceCode = mfg.ExternalId,
                EventType = EventType.ManufacturingProductOrder,
                Status = mfg.Status,
                Note = $"Created Manufacturing Order {mfg.ExternalId} from Merchandise Order {order.ExternalId}",
                CreatedDate = now
            }, cancellationToken);

            createdCount++;
        }

        return OperationResult<int>.Ok(createdCount);
    }

    /// <summary>
    /// Tải theo batch product, công thức VU/VA còn hiệu lực, vật tư và giá supplier để fail sớm,
    /// chọn đúng nguồn công thức và tránh N+1 khi một SaleOrder có nhiều dòng.
    /// </summary>
    private async Task<OperationResult<ManufacturingContext>> BuildContextAsync(
        Guid companyId,
        IReadOnlyCollection<MerchandiseOrderDetail> details,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var productIds = details.Select(x => x.ProductId).Distinct().ToArray();
        var vuFormulaIds = details.Select(x => x.FormulaId).Distinct().ToArray();

        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId) && x.CompanyId == companyId && x.IsActive)
            .Select(x => new ProductContext(
                x.ProductId,
                x.ColourCode,
                x.Name,
                x.ColourName))
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);

        var missingProductIds = productIds.Where(x => !products.ContainsKey(x)).ToArray();
        if (missingProductIds.Length > 0)
        {
            return OperationResult<ManufacturingContext>.Fail(
                $"Không tìm thấy sản phẩm active trong công ty: {string.Join(", ", missingProductIds)}.");
        }

        var existingVuFormulaIds = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                vuFormulaIds.Contains(x.FormulaId) &&
                x.IsActive &&
                x.CompanyId == companyId)
            .Select(x => x.FormulaId)
            .ToListAsync(cancellationToken);
        var existingVuFormulaIdSet = existingVuFormulaIds.ToHashSet();
        var missingFormulaIds = vuFormulaIds.Where(x => !existingVuFormulaIdSet.Contains(x)).ToArray();
        if (missingFormulaIds.Length > 0)
        {
            return OperationResult<ManufacturingContext>.Fail(
                $"Không tìm thấy công thức VU active trong công ty: {string.Join(", ", missingFormulaIds)}.");
        }

        var validStandardFormulaRows = await (
                from standard in _dbContext.ProductStandardFormulas.AsNoTracking()
                join formula in _dbContext.ManufacturingFormulas.AsNoTracking()
                    on standard.ManufacturingFormulaId equals formula.ManufacturingFormulaId
                where productIds.Contains(standard.ProductId) &&
                      standard.CompanyId == companyId &&
                      standard.ValidFrom <= now &&
                      (!standard.ValidTo.HasValue || standard.ValidTo.Value >= now) &&
                      formula.CompanyId == companyId &&
                      formula.IsActive
                select new StandardFormulaContext(
                    standard.ProductId,
                    formula.ManufacturingFormulaId,
                    formula.ExternalId,
                    formula.SourceVUFormulaId,
                    formula.SourceVUExternalIdSnapshot,
                    standard.ValidFrom))
            .ToListAsync(cancellationToken);

        var standardFormulaByProduct = validStandardFormulaRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.ValidFrom)
                    .ThenByDescending(x => x.FormulaId)
                    .First());

        var formulaMaterialsByVu = await LoadVuFormulaMaterialsAsync(
            vuFormulaIds,
            cancellationToken);
        var standardFormulaIds = standardFormulaByProduct.Values
            .Select(x => x.FormulaId)
            .Distinct()
            .ToArray();
        var formulaMaterialsByVa = await LoadVaFormulaMaterialsAsync(
            standardFormulaIds,
            cancellationToken);

        var materialIds = formulaMaterialsByVu.Values
            .Concat(formulaMaterialsByVa.Values)
            .SelectMany(x => x)
            .Where(x => x.IsMaterial && x.ItemId != Guid.Empty)
            .Select(x => x.ItemId)
            .Distinct()
            .ToArray();
        var materialPrices = await LoadMaterialPricesAsync(
            companyId,
            materialIds,
            cancellationToken);

        return OperationResult<ManufacturingContext>.Ok(new ManufacturingContext(
            products,
            existingVuFormulaIdSet,
            standardFormulaByProduct,
            formulaMaterialsByVu,
            formulaMaterialsByVa,
            materialPrices));
    }

    /// <summary>
    /// Tải và nhóm các thành phần đang hoạt động theo công thức VU.
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<FormulaMaterialContext>>> LoadVuFormulaMaterialsAsync(
        IReadOnlyCollection<Guid> formulaIds,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x => formulaIds.Contains(x.FormulaId) && x.IsActive)
            .Select(x => new FormulaMaterialRow(
                x.FormulaId,
                x.itemType,
                x.MaterialId,
                x.ProductId,
                x.Quantity,
                x.Unit,
                x.UnitPrice))
            .ToListAsync(cancellationToken);

        return GroupFormulaMaterials(rows);
    }

    /// <summary>
    /// Tải và nhóm các thành phần đang hoạt động theo công thức VA chuẩn.
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<FormulaMaterialContext>>> LoadVaFormulaMaterialsAsync(
        IReadOnlyCollection<Guid> formulaIds,
        CancellationToken cancellationToken)
    {
        if (formulaIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<FormulaMaterialContext>>();
        }

        var rows = await _dbContext.ManufacturingFormulaMaterials
            .AsNoTracking()
            .Where(x => formulaIds.Contains(x.ManufacturingFormulaId) && x.IsActive)
            .Select(x => new FormulaMaterialRow(
                x.ManufacturingFormulaId,
                x.itemType,
                x.MaterialId,
                x.ProductId,
                x.Quantity,
                x.Unit,
                x.UnitPrice))
            .ToListAsync(cancellationToken);

        return GroupFormulaMaterials(rows);
    }

    /// <summary>
    /// Chọn giá supplier ưu tiên hoặc giá cập nhật gần nhất của từng vật tư trong company.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> LoadMaterialPricesAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> materialIds,
        CancellationToken cancellationToken)
    {
        if (materialIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var rows = await _dbContext.MaterialsSuppliers
            .AsNoTracking()
            .Where(x =>
                materialIds.Contains(x.MaterialId) &&
                x.Material.CompanyId == companyId &&
                x.IsActive != false)
            .Select(x => new MaterialPriceRow(
                x.MaterialId,
                x.CurrentPrice ?? 0m,
                x.IsPreferred == true,
                x.UpdatedDate ?? x.CreateDate))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.MaterialId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.IsPreferred)
                    .ThenByDescending(x => x.UpdatedAt)
                    .First()
                    .Price);
    }

    /// <summary>
    /// Chuẩn hóa material/product component và nhóm chúng theo công thức nguồn.
    /// </summary>
    private static Dictionary<Guid, IReadOnlyList<FormulaMaterialContext>> GroupFormulaMaterials(
        IReadOnlyCollection<FormulaMaterialRow> rows)
    {
        return rows
            .GroupBy(x => x.FormulaId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FormulaMaterialContext>)group
                    .Select(x => new FormulaMaterialContext(
                        x.ItemType is ItemType.Material or ItemType.MaterialFailure
                            ? x.MaterialId ?? Guid.Empty
                            : x.ProductId ?? Guid.Empty,
                        x.ItemType is ItemType.Material or ItemType.MaterialFailure,
                        x.Quantity,
                        x.Unit,
                        x.UnitPrice))
                    .ToList());
    }

    /// <summary>
    /// Snapshot dữ liệu nền dùng chung cho toàn bộ dòng hàng trong một lần tạo MFG.
    /// </summary>
    private sealed class ManufacturingContext
    {
        private readonly IReadOnlySet<Guid> _vuFormulaIds;
        private readonly IReadOnlyDictionary<Guid, StandardFormulaContext> _standardFormulaByProduct;
        private readonly IReadOnlyDictionary<Guid, IReadOnlyList<FormulaMaterialContext>> _formulaMaterialsByVu;
        private readonly IReadOnlyDictionary<Guid, IReadOnlyList<FormulaMaterialContext>> _formulaMaterialsByVa;
        private readonly IReadOnlyDictionary<Guid, decimal> _materialPrices;

        public ManufacturingContext(
            IReadOnlyDictionary<Guid, ProductContext> products,
            IReadOnlySet<Guid> vuFormulaIds,
            IReadOnlyDictionary<Guid, StandardFormulaContext> standardFormulaByProduct,
            IReadOnlyDictionary<Guid, IReadOnlyList<FormulaMaterialContext>> formulaMaterialsByVu,
            IReadOnlyDictionary<Guid, IReadOnlyList<FormulaMaterialContext>> formulaMaterialsByVa,
            IReadOnlyDictionary<Guid, decimal> materialPrices)
        {
            Products = products;
            _vuFormulaIds = vuFormulaIds;
            _standardFormulaByProduct = standardFormulaByProduct;
            _formulaMaterialsByVu = formulaMaterialsByVu;
            _formulaMaterialsByVa = formulaMaterialsByVa;
            _materialPrices = materialPrices;
        }

        public IReadOnlyDictionary<Guid, ProductContext> Products { get; }

        /// <summary>
        /// Ưu tiên công thức VA chuẩn còn hiệu lực của product, nếu không có thì fallback về VU của dòng đơn.
        /// </summary>
        public bool TryResolveFormula(
            MerchandiseOrderDetail detail,
            out ResolvedFormulaContext resolvedFormula)
        {
            if (_standardFormulaByProduct.TryGetValue(detail.ProductId, out var standardFormula))
            {
                _formulaMaterialsByVa.TryGetValue(standardFormula.FormulaId, out var materials);
                resolvedFormula = BuildResolvedFormula(
                    standardFormula.FormulaId,
                    true,
                    materials ?? Array.Empty<FormulaMaterialContext>());
                return true;
            }

            if (!_vuFormulaIds.Contains(detail.FormulaId))
            {
                resolvedFormula = default!;
                return false;
            }

            _formulaMaterialsByVu.TryGetValue(detail.FormulaId, out var vuMaterials);
            resolvedFormula = BuildResolvedFormula(
                detail.FormulaId,
                false,
                vuMaterials ?? Array.Empty<FormulaMaterialContext>());
            return true;
        }

        private ResolvedFormulaContext BuildResolvedFormula(
            Guid formulaId,
            bool usesStandardFormula,
            IReadOnlyList<FormulaMaterialContext> materials)
        {
            var materialCosts = materials
                .Where(x => x.IsMaterial)
                .Sum(x => x.Quantity * (_materialPrices.GetValueOrDefault(x.ItemId, x.UnitPrice)));

            return new ResolvedFormulaContext(
                formulaId,
                usesStandardFormula,
                materials,
                materialCosts);
        }
    }

    private sealed record ProductContext(
        Guid ProductId,
        string? ColourCode,
        string? Name,
        string? ColourName);

    private sealed record StandardFormulaContext(
        Guid ProductId,
        Guid FormulaId,
        string ExternalId,
        Guid? SourceVuFormulaId,
        string? SourceVuExternalId,
        DateTime ValidFrom);

    private sealed record FormulaMaterialRow(
        Guid FormulaId,
        ItemType ItemType,
        Guid? MaterialId,
        Guid? ProductId,
        decimal Quantity,
        string? Unit,
        decimal UnitPrice);

    private sealed record FormulaMaterialContext(
        Guid ItemId,
        bool IsMaterial,
        decimal Quantity,
        string? Unit,
        decimal UnitPrice);

    private sealed record MaterialPriceRow(
        Guid MaterialId,
        decimal Price,
        bool IsPreferred,
        DateTime? UpdatedAt);

    private sealed record ResolvedFormulaContext(
        Guid FormulaId,
        bool UsesStandardFormula,
        IReadOnlyList<FormulaMaterialContext> Materials,
        decimal MaterialCost);
}
