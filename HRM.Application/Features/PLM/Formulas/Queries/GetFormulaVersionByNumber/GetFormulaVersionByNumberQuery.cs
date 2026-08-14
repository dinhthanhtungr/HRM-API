using HRM.Application.Features.PLM.Formulas.Dtos.Versions;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaVersionByNumber;

public sealed record GetFormulaVersionByNumberQuery(Guid FormulaId, int VersionNo)
    : IRequest<FormulaVersionDto?>;
