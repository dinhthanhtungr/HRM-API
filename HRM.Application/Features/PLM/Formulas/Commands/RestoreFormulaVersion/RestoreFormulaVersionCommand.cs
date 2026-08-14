using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Versions;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Commands.RestoreFormulaVersion;

public sealed record RestoreFormulaVersionCommand(
    Guid FormulaId,
    int VersionNo,
    RestoreFormulaVersionRequest Request)
    : IRequest<OperationResult<FormulaVersionActionResultDto>>;
