using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Formulas.Dtos.GetFormulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulas
{
    internal sealed class GetFormulasQueryHandler
        : IRequestHandler<GetFormulasQuery, FormulaList>
    {
        private readonly IPLMReadDbContext _dbContext;

        public GetFormulasQueryHandler(IPLMReadDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<FormulaList> Handle(
            GetFormulasQuery request,
            CancellationToken cancellationToken)
        {
            var productId = await ResolveProductIdAsync(request, cancellationToken);
            if (productId == Guid.Empty)
            {
                return new FormulaList();
            }

            var result = new FormulaList
            {
                FormulaSelects = await GetFormulaSelectsAsync(request, productId, cancellationToken),
                FormulaDevs = await GetFormulaDevsAsync(request, productId, cancellationToken),
                FormulaStandard = await GetFormulaStandardAsync(request, productId, cancellationToken)
            };

            if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
            {
                result.FormulaSelects = ApplyKeyword(result.FormulaSelects, request.NormalizedKeyword);
                result.FormulaDevs = ApplyKeyword(result.FormulaDevs, request.NormalizedKeyword);
                result.FormulaStandard = ApplyKeyword(result.FormulaStandard, request.NormalizedKeyword);
            }

            return result;
        }

        private async Task<Guid> ResolveProductIdAsync(
            GetFormulasQuery request,
            CancellationToken cancellationToken)
        {
            if (request.ProductId is { } productId && productId != Guid.Empty)
            {
                return productId;
            }

            if (request.SampleRequestId is not { } sampleRequestId || sampleRequestId == Guid.Empty)
            {
                return Guid.Empty;
            }

            return await _dbContext.SampleRequests
                .AsNoTracking()
                .Where(x => x.SampleRequestId == sampleRequestId && x.IsActive)
                .Select(x => x.ProductId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<IReadOnlyList<FormulaId>> GetFormulaSelectsAsync(
            GetFormulasQuery request,
            Guid productId,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.ProductionSelectVersions
                .AsNoTracking()
                .Where(x =>
                    x.ManufacturingFormulaId.HasValue &&
                    x.ManufacturingFormula != null &&
                    x.ManufacturingFormula.IsActive &&
                    x.MfgProductionOrder.IsActive &&
                    x.MfgProductionOrder.ProductId == productId);

            if (request.CompanyId is { } companyId && companyId != Guid.Empty)
            {
                query = query.Where(x => x.CompanyId == companyId);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(x => x.ManufacturingFormula!.Status == request.Status);
            }

            if (request.FormulaId is { } formulaId && formulaId != Guid.Empty)
            {
                query = query.Where(x => x.ManufacturingFormulaId == formulaId);
            }

            var rows = await query
                .Select(x => new
                {
                    Id = x.ManufacturingFormulaId!.Value,
                    ExternalId = x.ManufacturingFormula!.ExternalId,
                    Note = x.ManufacturingFormula.Note,
                    Price = x.ManufacturingFormula.TotalPrice ?? 0m,
                    ItemCount = x.ManufacturingFormula.ManufacturingFormulaMaterials.Count(m => m.IsActive),
                    LastDateUse = x.MfgProductionOrder.ManufacturingDate
                        ?? x.MfgProductionOrder.UpdatedDate
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(x => x.Id)
                .Select(x => x
                    .OrderByDescending(f => f.LastDateUse)
                    .First())
                .Select(x => new FormulaId
                {
                    Id = x.Id,
                    ExternalId = x.ExternalId,
                    Note = x.Note ?? string.Empty,
                    Price = x.Price,
                    ItemCount = x.ItemCount,
                    LastDateUse = x.LastDateUse
                })
                .OrderByDescending(x => x.LastDateUse)
                .ToList();
        }

        private async Task<IReadOnlyList<FormulaId>> GetFormulaDevsAsync(
            GetFormulasQuery request,
            Guid productId,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.Formulas
                .AsNoTracking()
                .Where(x => x.IsActive && x.ProductId == productId);

            if (request.CompanyId is { } companyId && companyId != Guid.Empty)
            {
                query = query.Where(x => x.CompanyId == companyId);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(x => x.Status == request.Status);
            }

            if (request.FormulaId is { } formulaId && formulaId != Guid.Empty)
            {
                query = query.Where(x => x.FormulaId == formulaId);
            }

            return await query
                .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                .Select(x => new FormulaId
                {
                    Id = x.FormulaId,
                    ExternalId = x.ExternalId,
                    Note = x.Note ?? string.Empty,
                    Price = x.TotalPrice,
                    ItemCount = x.FormulaMaterials.Count(m => m.IsActive),
                    LastDateUse = x.UpdatedDate ?? x.CreatedDate
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<IReadOnlyList<FormulaId>> GetFormulaStandardAsync(
            GetFormulasQuery request,
            Guid productId,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.ProductStandardFormulas
                .AsNoTracking()
                .Where(x =>
                    x.ProductId == productId &&
                    x.ManufacturingFormulaId.HasValue &&
                    x.ManufacturingFormula != null &&
                    x.ManufacturingFormula.IsActive);

            if (request.CompanyId is { } companyId && companyId != Guid.Empty)
            {
                query = query.Where(x => x.CompanyId == companyId);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(x => x.ManufacturingFormula!.Status == request.Status);
            }

            if (request.FormulaId is { } formulaId && formulaId != Guid.Empty)
            {
                query = query.Where(x => x.ManufacturingFormulaId == formulaId);
            }

            var rows = await query
                .Select(x => new
                {
                    Id = x.ManufacturingFormulaId!.Value,
                    ExternalId = x.ManufacturingFormula!.ExternalId,
                    Note = x.ManufacturingFormula.Note,
                    Price = x.ManufacturingFormula.TotalPrice ?? 0m,
                    ItemCount = x.ManufacturingFormula.ManufacturingFormulaMaterials.Count(m => m.IsActive),
                    LastDateUse = (DateTime?)x.ValidFrom,
                    IsCurrent = x.ValidTo == null
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(x => x.Id)
                .Select(x => x
                    .OrderByDescending(f => f.IsCurrent)
                    .ThenByDescending(f => f.LastDateUse)
                    .First())
                .Select(x => new FormulaId
                {
                    Id = x.Id,
                    ExternalId = x.ExternalId,
                    Note = x.Note ?? string.Empty,
                    Price = x.Price,
                    ItemCount = x.ItemCount,
                    LastDateUse = x.LastDateUse
                })
                .OrderByDescending(x => rows.Any(r => r.Id == x.Id && r.IsCurrent))
                .ThenByDescending(x => x.LastDateUse)
                .ToList();
        }

        private static IReadOnlyList<FormulaId> ApplyKeyword(
            IReadOnlyList<FormulaId> formulas,
            string keyword)
        {
            return formulas
                .Where(x =>
                    x.ExternalId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    x.Note.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
