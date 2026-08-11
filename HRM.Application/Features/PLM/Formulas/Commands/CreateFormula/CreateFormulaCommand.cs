using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Commands.CreateFormula;

public sealed record CreateFormulaCommand(UpsertFormulaRequest Request)
    : IRequest<OperationResult<FormulaWriteResultDto>>;
