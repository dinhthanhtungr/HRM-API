using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingTemplateOptions;

public sealed record GetManufacturingTemplateOptionsQuery(bool IsProcessTemplate, string? Keyword, DateTime? EffectiveOn) : IRequest<IReadOnlyList<ManufacturingTemplateOptionDto>>;
