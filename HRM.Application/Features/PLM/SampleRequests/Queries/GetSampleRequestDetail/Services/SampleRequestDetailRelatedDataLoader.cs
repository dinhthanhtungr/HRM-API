using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Common;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Detail;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDetail.Services;

internal static class SampleRequestDetailRelatedDataLoader
{
    public static async Task PopulateAsync(
        IPLMReadDbContext dbContext,
        SampleRequestDetailDto detail,
        bool canViewFormulaPrices,
        CancellationToken cancellationToken)
    {
        var metadata = await dbContext.SampleRequests
            .AsNoTracking()
            .Where(x => x.SampleRequestId == detail.Hero.SampleRequestId)
            .Select(x => new
            {
                x.ProductId
            })
            .FirstAsync(cancellationToken);

        detail.FormulaLookups = await dbContext.Formulas
            .AsNoTracking()
            .Where(x => x.IsActive && x.ProductId == metadata.ProductId)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new SampleRequestDetailFormulaLookupDto
            {
                FormulaId = x.FormulaId,
                ExternalId = x.ExternalId,
                Name = x.Name,
                TotalPrice = canViewFormulaPrices ? x.TotalPrice : null
            })
            .ToListAsync(cancellationToken);

        detail.QuickSummary.DevelopmentFormulaCount = detail.FormulaLookups.Count;
    }

    private static async Task<IReadOnlyList<SampleRequestAttachmentDto>> GetAttachmentsAsync(
        IPLMReadDbContext dbContext,
        Guid attachmentCollectionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.AttachmentModels
            .AsNoTracking()
            .Where(x => x.IsActive && x.AttachmentCollectionId == attachmentCollectionId)
            .OrderBy(x => x.CreateDate)
            .Select(x => new SampleRequestAttachmentDto
            {
                AttachmentId = x.AttachmentId,
                AttachmentCollectionId = x.AttachmentCollectionId,
                Slot = x.Slot,
                FileName = x.FileName,
                SizeBytes = x.SizeBytes,
                Url = AttachmentFileHelper.BuildUrl(x.AttachmentId),
                DownloadUrl = AttachmentFileHelper.BuildDownloadUrl(x.AttachmentId),
                IsImage = AttachmentFileHelper.IsImageFile(x.FileName),
                CreateDate = x.CreateDate,
                CreateBy = x.CreateBy
            })
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<FormulaListDto>> GetFormulaAsync(
        IPLMReadDbContext dbContext,
        Guid productId,
        bool canViewFormulaPrices,
        CancellationToken cancellationToken)
    {
        return await dbContext.Formulas
            .AsNoTracking()
            .Where(x => x.IsActive && x.ProductId == productId)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new FormulaListDto
            {
                FormulaId = x.FormulaId,
                ExternalId = x.ExternalId,
                Name = x.Name,
                Status = x.Status,
                Note = x.Note ?? "_",
                TotalPrice = canViewFormulaPrices ? x.TotalPrice : null,
                ProductionPrice = canViewFormulaPrices ? x.ProductionPrice : null,
                PresidentPrice = canViewFormulaPrices ? x.PresidentPrice : null,
                ProfitMarginPrice = canViewFormulaPrices ? x.ProfitMarginPrice : null,
                EffectiveDate = x.EffectiveDate,
                IsSelect = x.IsSelect,
                MaterialCount = x.FormulaMaterials.Count(m => m.IsActive),
                MaterialsUrl = $"/api/v1/plm/formulas/{x.FormulaId}/materials"
            })
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<SampleRequestDetailManufacturingFormulaDto>> GetManufacturingFormulasAsync(
        IPLMReadDbContext dbContext,
        Guid productId,
        bool canViewFormulaPrices,
        CancellationToken cancellationToken)
    {
        var formulas = await dbContext.ManufacturingFormulas
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                ((x.SourceVUFormula != null && x.SourceVUFormula.ProductId == productId) ||
                    x.ProductStandardFormulas.Any(s => s.ProductId == productId) ||
                    x.ProductionSelectVersions.Any(s =>
                        s.MfgProductionOrder.ProductId == productId &&
                        s.ManufacturingFormulaId.HasValue)))
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new
            {
                x.ManufacturingFormulaId,
                x.ExternalId,
                x.Name,
                x.Status,
                x.Note,
                x.TotalPrice,
                MaterialCount = x.ManufacturingFormulaMaterials.Count(m => m.IsActive),
                IsStandard = x.ProductStandardFormulas.Any(s => s.ProductId == productId && s.ValidTo == null),
                IsSelectedInProductionOrder = x.ProductionSelectVersions.Any(s =>
                    s.MfgProductionOrder.ProductId == productId &&
                    s.ValidFrom != null &&
                    s.ValidTo == null),
                x.SourceVUFormulaId,
                x.SourceVUExternalIdSnapshot,
                x.SourceManufacturingFormulaId,
                x.SourceManufacturingExternalIdSnapshot,
                x.SourceType,
                x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return formulas
            .Select(x => new SampleRequestDetailManufacturingFormulaDto
            {
                ManufacturingFormulaId = x.ManufacturingFormulaId,
                ExternalId = x.ExternalId,
                Name = x.Name,
                Status = x.Status,
                Note = x.Note,
                TotalPrice = canViewFormulaPrices ? x.TotalPrice : null,
                MaterialCount = x.MaterialCount,
                MaterialsUrl = $"/api/v1/plm/manufacturing-formulas/{x.ManufacturingFormulaId}/materials",
                IsStandard = x.IsStandard,
                IsSelectedInProductionOrder = x.IsSelectedInProductionOrder,
                SourceVUFormulaId = x.SourceVUFormulaId,
                SourceVUExternalIdSnapshot = x.SourceVUExternalIdSnapshot,
                SourceManufacturingFormulaId = x.SourceManufacturingFormulaId,
                SourceManufacturingExternalIdSnapshot = x.SourceManufacturingExternalIdSnapshot,
                SourceType = x.SourceType.ToString(),
                CreatedDate = x.CreatedDate
            })
            .ToList();
    }

    private static async Task<IReadOnlyList<SampleRequestProductionOrderDto>> GetProductionOrdersAsync(
        IPLMReadDbContext dbContext,
        Guid productId,
        bool canViewFormulaPrices,
        CancellationToken cancellationToken)
    {
        var productionOrders = await dbContext.MfgProductionOrders
            .AsNoTracking()
            .Where(x => x.IsActive && x.ProductId == productId)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new
            {
                x.ProductId,
                x.MfgProductionOrderId,
                x.ExternalId,
                FormulaExternalId = x.FormulaExternalIdSnapshot ?? string.Empty
            })
            .Take(20)
            .ToListAsync(cancellationToken);

        var orderIds = productionOrders.Select(x => x.MfgProductionOrderId).ToList();
        var selectedByOrderId = await GetSelectedManufacturingFormulasAsync(
            dbContext,
            orderIds,
            canViewFormulaPrices,
            cancellationToken);
        var standardFormula = await GetStandardManufacturingFormulaAsync(
            dbContext,
            productId,
            canViewFormulaPrices,
            cancellationToken);

        return productionOrders
            .Select(order =>
            {
                selectedByOrderId.TryGetValue(order.MfgProductionOrderId, out var selectedFormula);

                if (selectedFormula is not null && standardFormula is not null)
                {
                    selectedFormula.IsStandard =
                        selectedFormula.ManufacturingFormulaId == standardFormula.ManufacturingFormulaId;

                    if (selectedFormula.IsStandard)
                    {
                        selectedFormula = null;
                    }
                }

                return new SampleRequestProductionOrderDto
                {
                    MfgProductionOrderId = order.MfgProductionOrderId,
                    ExternalId = order.ExternalId,
                    FormulaExternalId = order.FormulaExternalId,
                    SelectedManufacturingFormula = selectedFormula,
                    StandardManufacturingFormula = standardFormula
                };
            })
            .ToList();
    }

    private static async Task<Dictionary<Guid, SampleRequestSelectedManufacturingFormulaDto>>
        GetSelectedManufacturingFormulasAsync(
            IPLMReadDbContext dbContext,
            IReadOnlyList<Guid> productionOrderIds,
            bool canViewFormulaPrices,
            CancellationToken cancellationToken)
    {
        if (productionOrderIds.Count == 0)
        {
            return new Dictionary<Guid, SampleRequestSelectedManufacturingFormulaDto>();
        }

        var formulas = await dbContext.ProductionSelectVersions
            .AsNoTracking()
            .Where(x =>
                productionOrderIds.Contains(x.MfgProductionOrderId) &&
                x.ValidFrom != null &&
                x.ValidTo == null &&
                x.ManufacturingFormulaId.HasValue)
            .Select(x => new
            {
                x.MfgProductionOrderId,
                Formula = new SampleRequestSelectedManufacturingFormulaDto
                {
                    ManufacturingFormulaId = x.ManufacturingFormulaId!.Value,
                    ValidFrom = x.ValidFrom,
                    FormulaExternalId = x.ManufacturingFormula != null
                        ? x.ManufacturingFormula.ExternalId
                        : string.Empty,
                    Name = x.ManufacturingFormula != null
                        ? x.ManufacturingFormula.Name
                        : string.Empty,
                    TotalPrice = x.ManufacturingFormula != null
                        ? canViewFormulaPrices ? x.ManufacturingFormula.TotalPrice : null
                        : null,
                    MaterialCount = x.ManufacturingFormula != null
                        ? x.ManufacturingFormula.ManufacturingFormulaMaterials.Count(m => m.IsActive)
                        : 0,
                    MaterialsUrl = $"/api/v1/plm/manufacturing-formulas/{x.ManufacturingFormulaId}/materials"
                }
            })
            .ToListAsync(cancellationToken);

        return formulas
            .GroupBy(x => x.MfgProductionOrderId)
            .ToDictionary(x => x.Key, x => x.First().Formula);
    }

    private static async Task<SampleRequestStandardManufacturingFormulaDto?> GetStandardManufacturingFormulaAsync(
        IPLMReadDbContext dbContext,
        Guid productId,
        bool canViewFormulaPrices,
        CancellationToken cancellationToken)
    {
        var current = await dbContext.ProductStandardFormulas
            .AsNoTracking()
            .Where(x => x.ProductId == productId && x.ValidTo == null && x.ManufacturingFormulaId.HasValue)
            .Select(x => new SampleRequestStandardManufacturingFormulaDto
            {
                ProductStandardFormulaId = x.ProductStandardFormulaId,
                ManufacturingFormulaId = x.ManufacturingFormulaId!.Value,
                FormulaExternalId = x.ManufacturingFormula != null
                    ? x.ManufacturingFormula.ExternalId
                    : string.Empty,
                Name = x.ManufacturingFormula != null
                    ? x.ManufacturingFormula.Name
                    : string.Empty,
                TotalPrice = x.ManufacturingFormula != null
                    ? canViewFormulaPrices ? x.ManufacturingFormula.TotalPrice : null
                    : null,
                MaterialCount = x.ManufacturingFormula != null
                    ? x.ManufacturingFormula.ManufacturingFormulaMaterials.Count(m => m.IsActive)
                    : 0,
                MaterialsUrl = $"/api/v1/plm/manufacturing-formulas/{x.ManufacturingFormulaId}/materials",
                ValidFrom = x.ValidFrom
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            return null;
        }

        var previous = await dbContext.ProductStandardFormulas
            .AsNoTracking()
            .Where(x => x.ProductId == productId && x.ValidTo != null && x.ManufacturingFormulaId.HasValue)
            .OrderByDescending(x => x.ValidFrom)
            .Select(x => new
            {
                ManufacturingFormulaId = x.ManufacturingFormulaId!.Value,
                FormulaExternalId = x.ManufacturingFormula != null
                    ? x.ManufacturingFormula.ExternalId
                    : string.Empty,
                FormulaName = x.ManufacturingFormula != null
                    ? x.ManufacturingFormula.Name
                    : string.Empty,
                TotalPrice = x.ManufacturingFormula != null
                    ? canViewFormulaPrices ? x.ManufacturingFormula.TotalPrice : null
                    : null,
                MaterialCount = x.ManufacturingFormula != null
                    ? x.ManufacturingFormula.ManufacturingFormulaMaterials.Count(m => m.IsActive)
                    : 0,
                MaterialsUrl = $"/api/v1/plm/manufacturing-formulas/{x.ManufacturingFormulaId}/materials",
                x.ValidFrom
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (previous is null)
        {
            return current;
        }

        current.PreviousManufacturingFormulaId = previous.ManufacturingFormulaId;
        current.PreviousFormulaExternalId = previous.FormulaExternalId;
        current.PreviousFormulaName = previous.FormulaName;
        current.PreviousTotalPrice = previous.TotalPrice;
        current.PreviousMaterialCount = previous.MaterialCount;
        current.PreviousMaterialsUrl = previous.MaterialsUrl;
        current.PreviousValidFrom = previous.ValidFrom;

        return current;
    }
}
