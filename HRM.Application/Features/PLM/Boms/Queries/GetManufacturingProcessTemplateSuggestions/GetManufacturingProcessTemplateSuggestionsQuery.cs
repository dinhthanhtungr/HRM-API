using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplateSuggestions;

public sealed record GetManufacturingProcessTemplateSuggestionsQuery(Guid? FormulaId, Guid? BomVersionId, DateTime? EffectiveOn)
    : IRequest<IReadOnlyList<ManufacturingProcessTemplateSuggestionDto>>;
