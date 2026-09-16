using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Formulas.Dtos.Lookup;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulaLookup.Models;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaLookup;

internal sealed class GetFormulaLookupQueryHandler
    : IRequestHandler<GetFormulaLookupQuery, PagedResult<FormulaLookupDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetFormulaLookupQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<FormulaLookupDto>> Handle(
        GetFormulaLookupQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return EmptyResult(request);
        }

        var productId = await ResolveProductIdAsync(request, companyId, cancellationToken);
        //var colorCode = await ResolveColorCodeAsync(productId, companyId, cancellationToken);
        var includeVu = request.SourceType is null or FormulaSource.Both or FormulaSource.FromVU;
        var includeVa = request.SourceType is null or FormulaSource.Both or FormulaSource.FromVA or FormulaSource.FromBom;

        if (!includeVu && !includeVa)
        {
            return EmptyResult(request);
        }

        var vuFormulas = _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                includeVu &&
                x.IsActive &&
                x.Product.CompanyId == companyId);

        var vaFormulas = _dbContext.ManufacturingFormulas
            .AsNoTracking()
            .Where(x =>
                includeVa &&
                x.IsActive &&
                x.CompanyId == companyId);

        if (request.SourceType == FormulaSource.FromBom)
        {
            vaFormulas = vaFormulas.Where(x => x.SourceBomVersionId.HasValue);
        }
        else if (request.SourceType == FormulaSource.FromVA)
        {
            vaFormulas = vaFormulas.Where(x => !x.SourceBomVersionId.HasValue);
        }

        if (productId is { } scopedProductId)
        {
            vuFormulas = vuFormulas.Where(x => x.ProductId == scopedProductId);
            vaFormulas = vaFormulas.Where(x =>
                (x.SourceBomVersion != null &&
                 x.SourceBomVersion.BomDefinition.ProductId == scopedProductId &&
                 x.SourceBomVersion.BomDefinition.CompanyId == companyId) ||
                (x.SourceVUFormula != null &&
                 x.SourceVUFormula.ProductId == scopedProductId &&
                 x.SourceVUFormula.Product.CompanyId == companyId) ||
                x.ProductStandardFormulas.Any(s =>
                    s.ProductId == scopedProductId &&
                    s.Product.CompanyId == companyId) ||
                x.ProductionSelectVersions.Any(s =>
                    s.MfgProductionOrder.ProductId == scopedProductId &&
                    s.MfgProductionOrder.Product.CompanyId == companyId &&
                    s.ManufacturingFormulaId.HasValue));
        }
        var statuses = NormalizeStatuses(request);
        if (statuses.Length > 0)
        {
            vuFormulas = vuFormulas.Where(x => statuses.Contains(x.Status));
            vaFormulas = vaFormulas.Where(x => statuses.Contains(x.Status));
        }

        if (request.NormalizedKeyword is { } keyword)
        {
            vuFormulas = vuFormulas.Where(x =>
                EF.Functions.ILike(x.ExternalId, $"%{keyword}%") ||
                EF.Functions.ILike(x.Name, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.Product.Name ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.Product.ColourCode ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                x.Product.SampleRequests.Any(sampleRequest =>
                    sampleRequest.IsActive &&
                    sampleRequest.CompanyId == companyId &&
                    EF.Functions.ILike(sampleRequest.ExternalId, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                EF.Functions.ILike((x.Note ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter));

            vaFormulas = vaFormulas.Where(x =>
                EF.Functions.ILike(x.ExternalId, $"%{keyword}%") ||
                EF.Functions.ILike(x.Name, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                (x.SourceBomVersion != null &&
                 (EF.Functions.ILike((x.SourceBomVersion.BomDefinition.Product.Name ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                  EF.Functions.ILike((x.SourceBomVersion.BomDefinition.Product.ColourCode ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                  x.SourceBomVersion.BomDefinition.Product.SampleRequests.Any(sampleRequest =>
                      sampleRequest.IsActive &&
                      sampleRequest.CompanyId == companyId &&
                      EF.Functions.ILike(sampleRequest.ExternalId, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)))) ||
                x.ProductionSelectVersions.Any(version =>
                    version.MfgProductionOrder != null &&
                    version.MfgProductionOrder.Product != null &&
                    (
                        EF.Functions.ILike((version.MfgProductionOrder.Product.Name ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                        EF.Functions.ILike((version.MfgProductionOrder.Product.ColourCode ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                        EF.Functions.ILike((version.MfgProductionOrder.ColorName ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                        version.MfgProductionOrder.Product.SampleRequests.Any(sampleRequest =>
                            sampleRequest.IsActive &&
                            sampleRequest.CompanyId == companyId &&
                            EF.Functions.ILike(sampleRequest.ExternalId, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                        version.MfgProductionOrder.Product.Formulas.Any(formula =>
                            formula.IsActive &&
                            formula.CompanyId == companyId &&
                            EF.Functions.ILike(formula.ExternalId, $"%{keyword}%"))
                    )) ||
                EF.Functions.ILike((x.Note ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter));
        }

        var vuRows = vuFormulas.Select(x => new FormulaLookupRow
        {
            FormulaId = x.FormulaId,
            ColorCode = x.Product.ColourCode ?? string.Empty,
            SourceType = FormulaSource.FromVU,
            ExternalId = x.ExternalId,
            Name = x.Name,
            Status = x.Status,
            Note = x.Note,
            MaterialCount = x.FormulaMaterials.Count(m => m.IsActive),
            CreatedDate = x.CreatedDate
        });

        var vaRows = vaFormulas.Select(x => new FormulaLookupRow
        {
            FormulaId = x.ManufacturingFormulaId,
            ColorCode =
                    x.SourceBomVersion != null
                        ? x.SourceBomVersion.BomDefinition.Product.ColourCode ?? string.Empty
                        : x.SourceVUFormula != null
                        ? x.SourceVUFormula.Product.ColourCode ?? string.Empty
                        : x.ProductStandardFormulas
                            .Where(s => s.ValidTo == null)
                            .OrderByDescending(s => s.ValidFrom)
                            .Select(s => s.Product.ColourCode)
                            .FirstOrDefault()
                        ?? x.ProductionSelectVersions
                            .Where(v => v.ValidTo == null)
                            .OrderByDescending(v => v.ValidFrom)
                            .Select(v => v.MfgProductionOrder.Product.ColourCode)
                            .FirstOrDefault()
                        ?? string.Empty,
            SourceType = x.SourceBomVersionId.HasValue ? FormulaSource.FromBom : FormulaSource.FromVA,
            ExternalId = x.ExternalId,
            Name = x.Name,
            Status = x.Status,
            Note = x.Note,
            MaterialCount = x.ManufacturingFormulaMaterials.Count(m => m.IsActive),
            CreatedDate = x.CreatedDate
        });

        var query = vuRows.Concat(vaRows);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.CreatedDate == null)
            .ThenByDescending(x => x.CreatedDate)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new FormulaLookupDto
            {
                FormulaId = x.FormulaId,
                ColorCode = x.ColorCode,
                SourceType = x.SourceType,
                ExternalId = x.ExternalId,
                Name = x.Name,
                Status = x.Status,
                Note = x.Note,
                MaterialCount = x.MaterialCount,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<FormulaLookupDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private async Task<Guid?> ResolveProductIdAsync(
        GetFormulaLookupQuery request,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (request.ProductId is { } productId && productId != Guid.Empty)
        {
            return productId;
        }

        if (request.SampleRequestId is not { } sampleRequestId || sampleRequestId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.SampleRequestId == sampleRequestId &&
                x.IsActive &&
                x.CompanyId == companyId)
            .Select(x => (Guid?)x.ProductId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    //private async Task<string> ResolveColorCodeAsync(
    //    Guid? productId,
    //    Guid companyId,
    //    CancellationToken cancellationToken)
    //{
    //    if (productId is not { } scopedProductId)
    //    {
    //        return string.Empty;
    //    }

    //    return await _dbContext.Products
    //        .AsNoTracking()
    //        .Where(x => x.ProductId == scopedProductId && x.CompanyId == companyId)
    //        .Select(x => x.ColourCode ?? string.Empty)
    //        .FirstOrDefaultAsync(cancellationToken)
    //        ?? string.Empty;
    //}

    private static PagedResult<FormulaLookupDto> EmptyResult(GetFormulaLookupQuery request)
    {
        return new PagedResult<FormulaLookupDto>(
            [],
            0,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private static string[] NormalizeStatuses(GetFormulaLookupQuery request)
    {
        var values = request.Statuses ?? [];
        return values
            .Append(request.Status)
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .Select(status => status!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }


}
