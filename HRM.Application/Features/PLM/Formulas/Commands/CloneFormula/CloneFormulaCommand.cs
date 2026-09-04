using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Commands.CloneFormula;

/// <summary>
/// Creates a Draft Formula by copying the source header, prices and active materials server-side.
/// </summary>
public sealed record CloneFormulaCommand(Guid SourceFormulaId)
    : IRequest<OperationResult<FormulaWriteResultDto>>;
