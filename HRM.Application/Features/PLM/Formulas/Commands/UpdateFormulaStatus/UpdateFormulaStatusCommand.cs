using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;

public sealed record UpdateFormulaStatusCommand(
    Guid FormulaId,
    UpdateFormulaStatusRequest Request)
    : IRequest<OperationResult<FormulaWriteResultDto>>;
