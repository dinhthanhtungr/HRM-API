using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Common;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary.Models;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary.Services;

internal static class SampleRequestSummaryRelatedDataLoader
{
    public static async Task PopulateAsync(
        IPLMReadDbContext dbContext,
        IReadOnlyList<SampleRequestSummaryProjection> rows,
        CancellationToken cancellationToken)
    {
        var productIds = GetProductIds(rows);
        var attachmentCollectionIds = GetAttachmentCollectionIds(rows);

        var attachmentsByCollectionId = await GetAttachmentsByCollectionIdAsync(
            dbContext,
            attachmentCollectionIds,
            cancellationToken);
        var selectedFormulasByProductId = await GetSelectedFormulasByProductIdAsync(
            dbContext,
            productIds,
            cancellationToken);
        var productionOrdersByProductId = await GetProductionOrdersByProductIdAsync(
            dbContext,
            productIds,
            cancellationToken);

        foreach (var row in rows)
        {
            if (attachmentsByCollectionId.TryGetValue(row.AttachmentCollectionId, out var attachments))
            {
                row.Summary.Attachments = attachments;
            }

            if (row.ProductId.HasValue
                && selectedFormulasByProductId.TryGetValue(row.ProductId.Value, out var selectedFormula))
            {
                row.Summary.SelectedFormula = selectedFormula;
            }

            if (row.ProductId.HasValue
                && productionOrdersByProductId.TryGetValue(row.ProductId.Value, out var productionOrders))
            {
                row.Summary.ProductionOrders = productionOrders;
            }
        }
    }

    private static IReadOnlyList<Guid> GetProductIds(
        IReadOnlyList<SampleRequestSummaryProjection> rows)
    {
        return rows
            .Where(x => x.ProductId.HasValue)
            .Select(x => x.ProductId!.Value)
            .Distinct()
            .ToList();
    }

    private static IReadOnlyList<Guid> GetAttachmentCollectionIds(IReadOnlyList<SampleRequestSummaryProjection> rows)
    {
        return rows
            .Select(x => x.AttachmentCollectionId)
            .Distinct()
            .ToList();
    }

    private static async Task<Dictionary<Guid, List<SampleRequestAttachmentDto>>> GetAttachmentsByCollectionIdAsync(
        IPLMReadDbContext dbContext,
        IReadOnlyList<Guid> attachmentCollectionIds,
        CancellationToken cancellationToken)
    {
        if (attachmentCollectionIds.Count == 0)
        {
            return new Dictionary<Guid, List<SampleRequestAttachmentDto>>();
        }

        var attachments = await dbContext.AttachmentModels
            .AsNoTracking()
            .Where(x => x.IsActive && attachmentCollectionIds.Contains(x.AttachmentCollectionId))
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

        return attachments
            .GroupBy(x => x.AttachmentCollectionId)
            .ToDictionary(x => x.Key, x => x.ToList());
    }

    private static async Task<Dictionary<Guid, List<SampleRequestProductionOrderDto>>> GetProductionOrdersByProductIdAsync(
        IPLMReadDbContext dbContext,
        IReadOnlyList<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, List<SampleRequestProductionOrderDto>>();
        }

        var productionOrders = await dbContext.MfgProductionOrders
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                productIds.Contains(x.ProductId))
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new ProductionOrderProjection
            {
                ProductId = x.ProductId,
                MfgProductionOrderId = x.MfgProductionOrderId,
                ExternalId = x.ExternalId,
                FormulaExternalId = x.FormulaExternalIdSnapshot ?? string.Empty
            })
            .Take(5)
            .ToListAsync(cancellationToken);

        var selectedFormulasByProductionOrderId = await GetSelectedManufacturingFormulasByProductionOrderIdAsync(
            dbContext,
            productionOrders.Select(x => x.MfgProductionOrderId).ToList(),
            cancellationToken);
        var standardFormulasByProductId = await GetStandardManufacturingFormulasByProductIdAsync(
            dbContext,
            productionOrders.Select(x => x.ProductId).Distinct().ToList(),
            cancellationToken);

        return productionOrders
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(productionOrder =>
                {
                    selectedFormulasByProductionOrderId.TryGetValue(
                        productionOrder.MfgProductionOrderId,
                        out var selectedFormula);
                    standardFormulasByProductId.TryGetValue(
                        productionOrder.ProductId,
                        out var standardFormula);

                    // Bỏ công thức chuẩn ra khỏi công thức sản xuất
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
                        MfgProductionOrderId = productionOrder.MfgProductionOrderId,
                        ExternalId = productionOrder.ExternalId,
                        FormulaExternalId = productionOrder.FormulaExternalId,
                        SelectedManufacturingFormula = selectedFormula,
                        StandardManufacturingFormula = standardFormula
                    };
                })
                .ToList());
    }

    private static async Task<Dictionary<Guid, SampleRequestSelectedManufacturingFormulaDto>>
        GetSelectedManufacturingFormulasByProductionOrderIdAsync(
            IPLMReadDbContext dbContext,
            IReadOnlyList<Guid> productionOrderIds,
            CancellationToken cancellationToken)
    {
        if (productionOrderIds.Count == 0)
        {
            return new Dictionary<Guid, SampleRequestSelectedManufacturingFormulaDto>();
        }

        var selectedFormulas = await dbContext.ProductionSelectVersions
            .AsNoTracking()
            .Where(x =>
                productionOrderIds.Contains(x.MfgProductionOrderId) &&
                x.ValidFrom != null &&
                x.ValidTo == null &&
                x.ManufacturingFormulaId.HasValue)
            .Select(x => new SelectedManufacturingFormulaProjection
            {
                MfgProductionOrderId = x.MfgProductionOrderId,
                Formula = new SampleRequestSelectedManufacturingFormulaDto
                {
                    ManufacturingFormulaId = x.ManufacturingFormulaId!.Value,
                    FormulaExternalId = x.ManufacturingFormula != null
                        ? x.ManufacturingFormula.ExternalId
                        : string.Empty,
                    Name = x.ManufacturingFormula != null
                        ? x.ManufacturingFormula.Name
                        : string.Empty,
                    TotalPrice = x.ManufacturingFormula != null
                        ? x.ManufacturingFormula.TotalPrice
                        : null,
                    MaterialCount = x.ManufacturingFormula != null
                        ? x.ManufacturingFormula.ManufacturingFormulaMaterials.Count(m => m.IsActive)
                        : 0,
                    MaterialsUrl = $"/api/v1/plm/manufacturing-formulas/{x.ManufacturingFormulaId}/materials"
                }
            })
            .ToListAsync(cancellationToken);

        return selectedFormulas
            .GroupBy(x => x.MfgProductionOrderId)
            .ToDictionary(x => x.Key, x => x.First().Formula);
    }

    private static async Task<Dictionary<Guid, SampleRequestStandardManufacturingFormulaDto>>
        GetStandardManufacturingFormulasByProductIdAsync(
            IPLMReadDbContext dbContext,
            IReadOnlyList<Guid> productIds,
            CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, SampleRequestStandardManufacturingFormulaDto>();
        }

        var currentRows = await dbContext.ProductStandardFormulas
            .AsNoTracking()
            .Where(x =>
                productIds.Contains(x.ProductId) &&
                x.ValidTo == null &&
                x.ManufacturingFormulaId.HasValue)
            .Select(x => new
            {
                x.ProductId,
                StandardFormula = new SampleRequestStandardManufacturingFormulaDto
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
                        ? x.ManufacturingFormula.TotalPrice
                        : null,
                    MaterialCount = x.ManufacturingFormula != null
                        ? x.ManufacturingFormula.ManufacturingFormulaMaterials.Count(m => m.IsActive)
                        : 0,
                    MaterialsUrl = $"/api/v1/plm/manufacturing-formulas/{x.ManufacturingFormulaId}/materials",
                    ValidFrom = x.ValidFrom
                }
            })
            .ToListAsync(cancellationToken);

        if (currentRows.Count == 0)
        {
            return new Dictionary<Guid, SampleRequestStandardManufacturingFormulaDto>();
        }

        var previousRows = await dbContext.ProductStandardFormulas
            .AsNoTracking()
            .Where(x =>
                productIds.Contains(x.ProductId) &&
                x.ValidTo != null &&
                x.ManufacturingFormulaId.HasValue)
            .OrderByDescending(x => x.ValidFrom)
            .Select(x => new
            {
                x.ProductId,
                ManufacturingFormulaId = x.ManufacturingFormulaId!.Value,
                FormulaExternalId = x.ManufacturingFormula != null
                    ? x.ManufacturingFormula.ExternalId
                    : string.Empty,
                FormulaName = x.ManufacturingFormula != null
                    ? x.ManufacturingFormula.Name
                    : string.Empty,
                TotalPrice = x.ManufacturingFormula != null
                    ? x.ManufacturingFormula.TotalPrice
                    : null,
                MaterialCount = x.ManufacturingFormula != null
                    ? x.ManufacturingFormula.ManufacturingFormulaMaterials.Count(m => m.IsActive)
                    : 0,
                MaterialsUrl = $"/api/v1/plm/manufacturing-formulas/{x.ManufacturingFormulaId}/materials",
                x.ValidFrom
            })
            .ToListAsync(cancellationToken);

        var previousByProductId = previousRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.First());

        var result = currentRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.First().StandardFormula);

        foreach (var item in result)
        {
            if (!previousByProductId.TryGetValue(item.Key, out var previous))
            {
                continue;
            }

            item.Value.PreviousManufacturingFormulaId = previous.ManufacturingFormulaId;
            item.Value.PreviousFormulaExternalId = previous.FormulaExternalId;
            item.Value.PreviousFormulaName = previous.FormulaName;
            item.Value.PreviousTotalPrice = previous.TotalPrice;
            item.Value.PreviousMaterialCount = previous.MaterialCount;
            item.Value.PreviousMaterialsUrl = previous.MaterialsUrl;
            item.Value.PreviousValidFrom = previous.ValidFrom;
        }

        return result;
    }

    private static async Task<Dictionary<Guid, SampleRequestSelectedFormulaDto>> GetSelectedFormulasByProductIdAsync(
        IPLMReadDbContext dbContext,
        IReadOnlyList<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, SampleRequestSelectedFormulaDto>();
        }

        var formulas = await dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                productIds.Contains(x.ProductId) && x.IsSelect)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new
            {
                x.ProductId,
                Formula = new SampleRequestSelectedFormulaDto
                {
                    FormulaId = x.FormulaId,
                    ExternalId = x.ExternalId,
                    Name = x.Name,
                    Note = x.Note,
                    TotalPrice = x.TotalPrice,
                    MaterialCount = x.FormulaMaterials.Count(m => m.IsActive),
                    MaterialsUrl = $"/api/v1/plm/formulas/{x.FormulaId}/materials",
                    CreatedDate = x.CreatedDate ?? DateTime.MinValue,
                }
            })
            .ToListAsync(cancellationToken);

        return formulas
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                x => x.Key,
                x => x.First().Formula);
    }
}
