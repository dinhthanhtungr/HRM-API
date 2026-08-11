using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Commands.CreateManufacturingVUFormula;

public sealed record CreateManufacturingVUFormulaCommand(
    CreateManufacturingVUFormulaRequest Request)
    : IRequest<OperationResult<ManufacturingVUFormulaWriteResultDto>>;
