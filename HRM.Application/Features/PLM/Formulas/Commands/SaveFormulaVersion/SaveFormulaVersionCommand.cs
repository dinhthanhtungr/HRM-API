using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Versions;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Commands.SaveFormulaVersion;

public sealed record SaveFormulaVersionCommand(
    Guid FormulaId,
    SaveFormulaVersionRequest Request)
    : IRequest<OperationResult<FormulaVersionActionResultDto>>;
