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

        //var productId = await ResolveProductIdAsync(request, companyId, cancellationToken);
        var includeVu = request.SourceType is null or FormulaSource.Both or FormulaSource.FromVU;
        var includeVa = request.SourceType is null or FormulaSource.Both or FormulaSource.FromVA;

        if (!includeVu && !includeVa)
        {
            return EmptyResult(request);
        }

        var vuFormulas = _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                includeVu &&
                x.IsActive &&
                x.CompanyId == companyId);

        var vaFormulas = _dbContext.ManufacturingFormulas
            .AsNoTracking()
            .Where(x =>
                includeVa &&
                x.IsActive &&
                x.CompanyId == companyId);

        //if (productId is { } scopedProductId && scopedProductId != Guid.Empty)
        //{
        //    vuFormulas = vuFormulas.Where(x => x.ProductId == scopedProductId);
        //    vaFormulas = vaFormulas.Where(x =>
        //        (x.SourceVUFormula != null && x.SourceVUFormula.ProductId == scopedProductId) ||
        //        x.ProductStandardFormulas.Any(s => s.ProductId == scopedProductId) ||
        //        x.ProductionSelectVersions.Any(s =>
        //            s.MfgProductionOrder.ProductId == scopedProductId &&
        //            s.ManufacturingFormulaId.HasValue));
        //}

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            vuFormulas = vuFormulas.Where(x => x.Status == status);
            vaFormulas = vaFormulas.Where(x => x.Status == status);
        }

        if (request.NormalizedKeyword is { } keyword)
        {
            vuFormulas = vuFormulas.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.Name.Contains(keyword) ||
                (x.Product.Name ?? string.Empty).Contains(keyword) ||
                (x.Product.ColourCode ?? string.Empty).Contains(keyword) ||
                (x.Note ?? string.Empty).Contains(keyword));

            vaFormulas = vaFormulas.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.Name.Contains(keyword) ||
                x.ProductionSelectVersions.Any(version =>
                    version.MfgProductionOrder != null &&
                    version.MfgProductionOrder.Product != null &&
                    (
                        (version.MfgProductionOrder.Product.Name ?? string.Empty).Contains(keyword) ||
                        (version.MfgProductionOrder.Product.ColourCode ?? string.Empty).Contains(keyword) ||
                        (version.MfgProductionOrder.ColorName ?? string.Empty).Contains(keyword)
                    )) ||
                (x.Note ?? string.Empty).Contains(keyword));
        }

        var vuRows = vuFormulas.Select(x => new FormulaLookupRow
        {
            FormulaId = x.FormulaId,
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
            SourceType = FormulaSource.FromVA,
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
            .OrderByDescending(x => x.CreatedDate)
            .ThenBy(x => x.SourceType)
            .ThenBy(x => x.ExternalId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new FormulaLookupDto
            {
                FormulaId = x.FormulaId,
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

    private static PagedResult<FormulaLookupDto> EmptyResult(GetFormulaLookupQuery request)
    {
        return new PagedResult<FormulaLookupDto>(
            [],
            0,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }


}
