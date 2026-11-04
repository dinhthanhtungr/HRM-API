using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.PLM.Formulas.Dtos.GetFormulas;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Common;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary.Models;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary.Services;

internal static class SampleRequestSummaryRelatedDataLoader
{
    public static async Task PopulateAsync(
        IPLMReadDbContext dbContext,
        IReadOnlyList<SampleRequestSummaryProjection> rows,
        bool canViewFormulaPrices,
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
            canViewFormulaPrices,
            cancellationToken);
        var formulaListsByProduct = await GetFormulaListsByProductAsync(
            dbContext,
            rows,
            canViewFormulaPrices,
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

            if (row.ProductId.HasValue && formulaListsByProduct.TryGetValue(
                    (row.ProductId.Value, row.CompanyId),
                    out var formulaList))
            {
                row.Summary.FormulaSelects = formulaList.FormulaSelects;
                row.Summary.FormulaDevs = formulaList.FormulaDevs;
                row.Summary.FormulaStandard = formulaList.FormulaStandard;
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

    private static async Task<Dictionary<(Guid ProductId, Guid CompanyId), FormulaList>> GetFormulaListsByProductAsync(
        IPLMReadDbContext dbContext,
        IReadOnlyList<SampleRequestSummaryProjection> rows,
        bool canViewFormulaPrices,
        CancellationToken cancellationToken)
    {
        var productKeys = rows
            .Where(x => x.ProductId.HasValue)
            .Select(x => (ProductId: x.ProductId!.Value, x.CompanyId))
            .Distinct()
            .ToList();

        if (productKeys.Count == 0)
        {
            return new Dictionary<(Guid ProductId, Guid CompanyId), FormulaList>();
        }

        var productIds = productKeys.Select(x => x.ProductId).Distinct().ToList();
        var companyIds = productKeys.Select(x => x.CompanyId).Distinct().ToList();

        var formulaSelectRows = await dbContext.ProductionSelectVersions
            .AsNoTracking()
            .Where(x =>
                x.ManufacturingFormulaId.HasValue &&
                x.ManufacturingFormula != null &&
                x.ManufacturingFormula.IsActive &&
                companyIds.Contains(x.CompanyId) &&
                x.MfgProductionOrder.IsActive &&
                productIds.Contains(x.MfgProductionOrder.ProductId))
            .Select(x => new
            {
                ProductId = x.MfgProductionOrder.ProductId,
                x.CompanyId,
                Id = x.ManufacturingFormulaId!.Value,
                x.ManufacturingFormula!.ExternalId,
                CreatedByName = x.ManufacturingFormula.CreatedByNavigation != null
                    ? x.ManufacturingFormula.CreatedByNavigation.FullName
                    : null,
                x.ManufacturingFormula.Note,
                Price = canViewFormulaPrices ? x.ManufacturingFormula.TotalPrice : null,
                ItemCount = x.ManufacturingFormula.ManufacturingFormulaMaterials.Count(m => m.IsActive),
                LastDateUse = x.MfgProductionOrder.ManufacturingDate
                    ?? x.MfgProductionOrder.UpdatedDate
            })
            .ToListAsync(cancellationToken);

        var formulaDevRows = await dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId.HasValue &&
                companyIds.Contains(x.CompanyId.Value) &&
                productIds.Contains(x.ProductId))
            .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .Select(x => new
            {
                x.ProductId,
                CompanyId = x.CompanyId!.Value,
                Formula = new FormulaId
                {
                    Id = x.FormulaId,
                    ExternalId = x.ExternalId,
                    Name = x.Name,
                    CreatedByName = x.CreatedByNavigation != null
                        ? x.CreatedByNavigation.FullName
                        : null,
                    Note = x.Note ?? string.Empty,
                    Status = x.Status,
                    Price = canViewFormulaPrices ? x.TotalPrice : null,
                    ItemCount = x.FormulaMaterials.Count(m => m.IsActive),
                    LastDateUse = x.UpdatedDate ?? x.CreatedDate
                }
            })
            .ToListAsync(cancellationToken);

        var formulaStandardRows = await dbContext.ProductStandardFormulas
            .AsNoTracking()
            .Where(x =>
                productIds.Contains(x.ProductId) &&
                x.ManufacturingFormulaId.HasValue &&
                x.ManufacturingFormula != null &&
                x.ManufacturingFormula.IsActive &&
                companyIds.Contains(x.CompanyId))
            .Select(x => new
            {
                x.ProductId,
                x.CompanyId,
                Id = x.ManufacturingFormulaId!.Value,
                x.ManufacturingFormula!.ExternalId,
                CreatedByName = x.ManufacturingFormula.CreatedByNavigation != null
                    ? x.ManufacturingFormula.CreatedByNavigation.FullName
                    : null,
                x.ManufacturingFormula.Note,
                Price = canViewFormulaPrices ? x.ManufacturingFormula.TotalPrice : null,
                ItemCount = x.ManufacturingFormula.ManufacturingFormulaMaterials.Count(m => m.IsActive),
                LastDateUse = (DateTime?)x.ValidFrom,
                IsCurrent = x.ValidTo == null
            })
            .ToListAsync(cancellationToken);

        var result = productKeys.ToDictionary(x => x, _ => new FormulaList());

        foreach (var group in formulaSelectRows.GroupBy(x => (x.ProductId, x.CompanyId)))
        {
            if (!result.TryGetValue(group.Key, out var formulaList))
            {
                continue;
            }

            formulaList.FormulaSelects = group
                .GroupBy(x => x.Id)
                .Select(x => x.OrderByDescending(f => f.LastDateUse).First())
                .Select(x => new FormulaId
                {
                    Id = x.Id,
                    ExternalId = x.ExternalId,
                    CreatedByName = x.CreatedByName,
                    Note = x.Note ?? string.Empty,
                    Price = x.Price,
                    ItemCount = x.ItemCount,
                    LastDateUse = x.LastDateUse
                })
                .OrderByDescending(x => x.LastDateUse)
                .ToList();
        }

        foreach (var group in formulaDevRows.GroupBy(x => (x.ProductId, x.CompanyId)))
        {
            if (result.TryGetValue(group.Key, out var formulaList))
            {
                formulaList.FormulaDevs = group.Select(x => x.Formula).ToList();
            }
        }

        foreach (var group in formulaStandardRows.GroupBy(x => (x.ProductId, x.CompanyId)))
        {
            if (!result.TryGetValue(group.Key, out var formulaList))
            {
                continue;
            }

            formulaList.FormulaStandard = group
                .GroupBy(x => x.Id)
                .Select(x => x
                    .OrderByDescending(f => f.IsCurrent)
                    .ThenByDescending(f => f.LastDateUse)
                    .First())
                .Select(x => new FormulaId
                {
                    Id = x.Id,
                    ExternalId = x.ExternalId,
                    CreatedByName = x.CreatedByName,
                    Note = x.Note ?? string.Empty,
                    Price = x.Price,
                    ItemCount = x.ItemCount,
                    LastDateUse = x.LastDateUse
                })
                .OrderByDescending(x => group.Any(r => r.Id == x.Id && r.IsCurrent))
                .ThenByDescending(x => x.LastDateUse)
                .ToList();
        }

        return result;
    }

    private static async Task<Dictionary<Guid, SampleRequestSelectedFormulaDto>> GetSelectedFormulasByProductIdAsync(
        IPLMReadDbContext dbContext,
        IReadOnlyList<Guid> productIds,
        bool canViewFormulaPrices,
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
                    TotalPrice = canViewFormulaPrices ? x.TotalPrice : null,
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
