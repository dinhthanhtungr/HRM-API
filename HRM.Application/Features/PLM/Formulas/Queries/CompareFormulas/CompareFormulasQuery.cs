using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Comparison;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Queries.CompareFormulas;

public sealed record CompareFormulasQuery(
    Guid BaseFormulaId,
    Guid ComparedFormulaId,
    string? Currency)
    : IRequest<OperationResult<FormulaComparisonDto>>;
