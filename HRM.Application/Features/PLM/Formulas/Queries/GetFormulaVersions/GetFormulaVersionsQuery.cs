using HRM.Application.Features.PLM.Formulas.Dtos.Versions;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaVersions;

public sealed record GetFormulaVersionsQuery(Guid FormulaId)
    : IRequest<IReadOnlyList<FormulaVersionDto>?>;
