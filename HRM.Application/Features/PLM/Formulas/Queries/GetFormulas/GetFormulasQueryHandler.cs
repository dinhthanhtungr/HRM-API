using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Formulas.Dtos.GetFormulas;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulas
{
    internal sealed class GetFormulasQueryHandler
        : IRequestHandler<GetFormulasQuery, FormulaList>
    {
        private readonly IPLMReadDbContext _dbContext;
        private readonly IPLMFieldVisibilityService _fieldVisibility;

        public GetFormulasQueryHandler(
            IPLMReadDbContext dbContext,
            IPLMFieldVisibilityService fieldVisibility)
        {
            _dbContext = dbContext;
            _fieldVisibility = fieldVisibility;
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

            var canViewFormulaPrices = _fieldVisibility.CanViewFormulaPrices();

            if (request.IsMerchadiseOrder)
            {
                return new FormulaList
                {
                    FormulaDevs = await GetFormulaDevsAsync(
                        request,
                        productId,
                        canViewFormulaPrices,
                        cancellationToken)
                };
            }

            var result = new FormulaList
            {
                FormulaSelects = await GetFormulaSelectsAsync(request, productId, canViewFormulaPrices, cancellationToken),
                FormulaDevs = await GetFormulaDevsAsync(request, productId, canViewFormulaPrices, cancellationToken),
                FormulaStandard = await GetFormulaStandardAsync(request, productId, canViewFormulaPrices, cancellationToken)
            };

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
            bool canViewFormulaPrices,
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

            if (request.NormalizedKeyword is { } keyword)
            {
                var pattern = $"%{keyword}%";
                query = query.Where(x =>
                    EF.Functions.ILike(x.ManufacturingFormula!.ExternalId, pattern) ||
                    EF.Functions.ILike(x.ManufacturingFormula.Note ?? string.Empty, pattern));
            }

            var rows = await query
                .Select(x => new
                {
                    Id = x.ManufacturingFormulaId!.Value,
                    ExternalId = x.ManufacturingFormula!.ExternalId,
                    CreatedByName = x.ManufacturingFormula.CreatedByNavigation != null
                        ? x.ManufacturingFormula.CreatedByNavigation.FullName
                        : null,
                    Note = x.ManufacturingFormula.Note,
                    Price = canViewFormulaPrices ? x.ManufacturingFormula.TotalPrice : null,
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
                    CreatedByName = x.CreatedByName,
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
            bool canViewFormulaPrices,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.Formulas
                .AsNoTracking()
                .Where(x => x.IsActive && x.ProductId == productId);

            if (request.IsMerchadiseOrder)
            {
                query = query.Where(x => x.SampleRequests.Any(sampleRequest =>
                    sampleRequest.IsActive &&
                    sampleRequest.ProductId == productId &&
                    sampleRequest.FormulaId == x.FormulaId &&
                    sampleRequest.Status == SampleRequestStatus.Completed.ToString()));
            }

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

            if (request.NormalizedKeyword is { } keyword)
            {
                var pattern = $"%{keyword}%";
                query = query.Where(x =>
                    EF.Functions.ILike(x.ExternalId, pattern) ||
                    EF.Functions.ILike(x.Note ?? string.Empty, pattern));
            }

            return await query
                .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                .Select(x => new FormulaId
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
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<IReadOnlyList<FormulaId>> GetFormulaStandardAsync(
            GetFormulasQuery request,
            Guid productId,
            bool canViewFormulaPrices,
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

            if (request.NormalizedKeyword is { } keyword)
            {
                var pattern = $"%{keyword}%";
                query = query.Where(x =>
                    EF.Functions.ILike(x.ManufacturingFormula!.ExternalId, pattern) ||
                    EF.Functions.ILike(x.ManufacturingFormula.Note ?? string.Empty, pattern));
            }

            var rows = await query
                .Select(x => new
                {
                    Id = x.ManufacturingFormulaId!.Value,
                    ExternalId = x.ManufacturingFormula!.ExternalId,
                    CreatedByName = x.ManufacturingFormula.CreatedByNavigation != null
                        ? x.ManufacturingFormula.CreatedByNavigation.FullName
                        : null,
                    Note = x.ManufacturingFormula.Note,
                    Price = canViewFormulaPrices ? x.ManufacturingFormula.TotalPrice : null,
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
                    CreatedByName = x.CreatedByName,
                    Note = x.Note ?? string.Empty,
                    Price = x.Price,
                    ItemCount = x.ItemCount,
                    LastDateUse = x.LastDateUse
                })
                .OrderByDescending(x => rows.Any(r => r.Id == x.Id && r.IsCurrent))
                .ThenByDescending(x => x.LastDateUse)
                .ToList();
        }

    }
}
