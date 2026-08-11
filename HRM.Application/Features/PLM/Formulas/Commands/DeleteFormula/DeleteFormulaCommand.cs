using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Commands.DeleteFormula;

public sealed record DeleteFormulaCommand(Guid FormulaId)
    : IRequest<OperationResult<FormulaWriteResultDto>>;
