using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.ProcessManufacturingFormula;

public sealed record ProcessManufacturingFormulaCommand(
    Guid Id,
    Guid FormulaId,
    ProcessManufacturingFormulaRequest Request
) : IRequest<OperationResult<ProcessManufacturingFormulaResult>>;
