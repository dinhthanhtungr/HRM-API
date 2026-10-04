using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplateSuggestions;

internal sealed class GetManufacturingProcessTemplateSuggestionsQueryHandler
    : IRequestHandler<GetManufacturingProcessTemplateSuggestionsQuery, IReadOnlyList<ManufacturingProcessTemplateSuggestionDto>>
{
    private readonly IPLMReadDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetManufacturingProcessTemplateSuggestionsQueryHandler(IPLMReadDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ManufacturingProcessTemplateSuggestionDto>> Handle(
        GetManufacturingProcessTemplateSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return [];

        var formulaId = request.FormulaId;
        if (!formulaId.HasValue && request.BomVersionId.HasValue)
        {
            formulaId = await _db.BomVersions.AsNoTracking()
                .Where(x => x.BomVersionId == request.BomVersionId.Value && x.BomDefinition.CompanyId == companyId)
                .Select(x => x.SourceFormulaId ??
                             (x.SourceEngineeringBomVersion != null
                                 ? x.SourceEngineeringBomVersion.SourceFormulaId
                                 : null))
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!formulaId.HasValue)
            return [];

        var formula = await _db.Formulas.AsNoTracking()
            .Where(x => x.FormulaId == formulaId.Value && x.CompanyId == companyId && x.IsActive &&
                        x.Product.CompanyId == companyId && x.Product.IsActive)
            .Select(x => new { x.StepOfProduct, x.Product.CategoryId })
            .FirstOrDefaultAsync(cancellationToken);
        if (formula is null)
            return [];

        var effectiveOn = request.EffectiveOn ?? DateTime.Now;
        var candidates = await _db.ManufacturingProcessTemplates.AsNoTracking()
            .Include(x => x.ApplicabilityRules)
            .Include(x => x.Stages)
            .Where(x => x.CompanyId == companyId && x.IsActive &&
                        x.Status == ManufacturingTemplateStatus.Released &&
                        (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= effectiveOn) &&
                        (!x.EffectiveTo.HasValue || x.EffectiveTo >= effectiveOn) &&
                        x.ApplicabilityRules.Any(rule =>
                            (!rule.CategoryId.HasValue || rule.CategoryId == formula.CategoryId) &&
                            (!rule.StepOfProduct.HasValue ||
                             formula.StepOfProduct.HasValue && rule.StepOfProduct == formula.StepOfProduct)))
            .ToListAsync(cancellationToken);

        return candidates
            .Select(template =>
            {
                var matches = template.ApplicabilityRules
                    .Where(rule =>
                        (!rule.CategoryId.HasValue || rule.CategoryId == formula.CategoryId) &&
                        (!rule.StepOfProduct.HasValue ||
                         formula.StepOfProduct.HasValue && rule.StepOfProduct == formula.StepOfProduct))
                    .Select(rule => new
                    {
                        Score = rule.CategoryId.HasValue && rule.StepOfProduct.HasValue ? 3
                            : rule.StepOfProduct.HasValue ? 2
                            : 1,
                        rule.Priority
                    })
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Priority)
                    .First();

                return new ManufacturingProcessTemplateSuggestionDto
                {
                    ManufacturingProcessTemplateId = template.ManufacturingProcessTemplateId,
                    ExternalId = template.ExternalId,
                    Name = template.Name,
                    VersionNo = template.VersionNo,
                    StageCount = template.Stages.Count,
                    MatchScore = matches.Score,
                    MatchLevel = matches.Score == 3 ? "Exact" : matches.Score == 2 ? "Route" : "Category",
                    Priority = matches.Priority
                };
            })
            .OrderByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.Priority)
            .ThenByDescending(x => x.VersionNo)
            .ThenBy(x => x.ExternalId)
            .ToList();
    }
}
