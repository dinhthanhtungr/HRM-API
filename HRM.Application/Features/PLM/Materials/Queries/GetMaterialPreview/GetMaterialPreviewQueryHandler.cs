using HRM.Application.Abstractions.Persistence.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Commons.Pricing.Rules;
using HRM.Application.Features.PLM.Materials.Dtos.Preview;
using HRM.Application.Features.Warehouse.Helpers.Publics;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialPreview;

internal sealed class GetMaterialPreviewQueryHandler
    : IRequestHandler<GetMaterialPreviewQuery, MaterialPreviewDto?>
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".webp"
    };

    private readonly IPLMReadDbContext _dbContext;
    private readonly IPriceReadDbContext _priceDbContext;
    private readonly IWarehouseReadDbContext _warehouseDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPLMFieldVisibilityService _fieldVisibility;

    public GetMaterialPreviewQueryHandler(
        IPLMReadDbContext dbContext,
        IPriceReadDbContext priceDbContext,
        IWarehouseReadDbContext warehouseDbContext,
        ICurrentUser currentUser,
        IPLMFieldVisibilityService fieldVisibility)
    {
        _dbContext = dbContext;
        _priceDbContext = priceDbContext;
        _warehouseDbContext = warehouseDbContext;
        _currentUser = currentUser;
        _fieldVisibility = fieldVisibility;
    }

    public async Task<MaterialPreviewDto?> Handle(
        GetMaterialPreviewQuery request,
        CancellationToken cancellationToken)
    {
        if (request.MaterialId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return null;
        }

        var material = await _dbContext.Materials
            .AsNoTracking()
            .Where(x =>
                x.MaterialId == request.MaterialId &&
                x.CompanyId == companyId &&
                x.IsActive == true)
            .Select(x => new MaterialPreviewProjection
            {
                MaterialId = x.MaterialId,
                ExternalId = x.ExternalId,
                CustomCode = x.CustomCode,
                Name = x.Name,
                CategoryName = x.Category.Name,
                AttachmentCollectionId = x.AttachmentCollectionId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
        {
            return null;
        }

        var attachments = material.AttachmentCollectionId is not { } collectionId ||
                          collectionId == Guid.Empty
            ? []
            : await LoadAttachmentsAsync(
                material.MaterialId,
                collectionId,
                cancellationToken);

        var lastPurchase = await LoadLastPurchaseAsync(
            material.MaterialId,
            companyId,
            _fieldVisibility.CanViewFormulaPrices(),
            cancellationToken);

        var totalOnHandKg = await LoadTotalOnHandKgAsync(
            material.ExternalId,
            companyId,
            cancellationToken);

        return new MaterialPreviewDto
        {
            MaterialId = material.MaterialId,
            ExternalId = material.ExternalId,
            CustomCode = material.CustomCode,
            Name = material.Name,
            CategoryName = material.CategoryName,
            TotalOnHandKg = totalOnHandKg,
            LastPurchase = lastPurchase,
            Attachments = attachments
        };
    }

    private async Task<decimal> LoadTotalOnHandKgAsync(
        string? materialCode,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(materialCode))
        {
            return 0m;
        }

        return await WarehouseStockQueryHelper
            .ForItemStock(
                _warehouseDbContext.WarehouseShelfStocks.AsNoTracking(),
                companyId,
                materialCode,
                StockType.RawMaterial,
                excludeMixingShelf: true)
            .SumAsync(stock => (decimal?)stock.QtyKg, cancellationToken) ?? 0m;
    }

    private async Task<MaterialLastPurchaseDto?> LoadLastPurchaseAsync(
        Guid materialId,
        Guid companyId,
        bool canViewPrice,
        CancellationToken cancellationToken)
    {
        return await _priceDbContext.PurchaseOrderDetails
            .AsNoTracking()
            .Where(x =>
                x.MaterialId == materialId &&
                x.IsActive &&
                x.PurchaseOrder != null &&
                x.PurchaseOrder.CompanyId == companyId &&
                (x.PurchaseOrder.IsActive ?? true) &&
                (x.PurchaseOrder.Status == null ||
                 !PurchaseOrderPriceRules.CanceledStatuses.Contains(x.PurchaseOrder.Status)))
            .OrderByDescending(x => x.PurchaseOrder.CreateDate)
            .ThenByDescending(x => x.LineNo)
            .Select(x => new MaterialLastPurchaseDto
            {
                PurchaseOrderId = x.PurchaseOrderId,
                PurchaseOrderCode = x.PurchaseOrder.ExternalId,
                SupplierName = x.PurchaseOrder.Supplier != null
                    ? x.PurchaseOrder.Supplier.SupplierName
                    : null,
                UnitPrice = canViewPrice ? x.UnitPriceAgreed : null,
                Quantity = x.RealQuantity ?? x.RequestQuantity,
                PurchaseDate = x.PurchaseOrder.CreateDate
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<MaterialPreviewAttachmentDto>> LoadAttachmentsAsync(
        Guid materialId,
        Guid collectionId,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.AttachmentModels
            .AsNoTracking()
            .Where(x =>
                x.AttachmentCollectionId == collectionId &&
                x.IsActive)
            .OrderBy(x => x.CreateDate)
            .Select(x => new
            {
                x.AttachmentId,
                x.Slot,
                x.FileName,
                x.SizeBytes,
                x.CreateDate
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(x =>
            {
                var extension = Path.GetExtension(x.FileName);
                var contentUrl = BuildContentUrl(materialId, x.AttachmentId);

                return new MaterialPreviewAttachmentDto
                {
                    AttachmentId = x.AttachmentId,
                    Slot = x.Slot,
                    FileName = x.FileName,
                    SizeBytes = x.SizeBytes,
                    IsImage = ImageExtensions.Contains(extension),
                    IsPdf = extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase),
                    ContentUrl = contentUrl,
                    DownloadUrl = $"{contentUrl}?mode=download",
                    CreatedDate = x.CreateDate
                };
            })
            .ToList();
    }

    private static string BuildContentUrl(Guid materialId, Guid attachmentId)
    {
        return $"/api/v1/plm/materials/{materialId}/attachments/{attachmentId}/content";
    }

    private sealed class MaterialPreviewProjection
    {
        public Guid MaterialId { get; init; }
        public string? ExternalId { get; init; }
        public string? CustomCode { get; init; }
        public string? Name { get; init; }
        public string? CategoryName { get; init; }
        public Guid? AttachmentCollectionId { get; init; }
    }
}
