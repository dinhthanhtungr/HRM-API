using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Commands.PatchManufacturingVUFormula;

public sealed record PatchManufacturingVUFormulaCommand(
    Guid ManufacturingVUFormulaId,
    PatchManufacturingVUFormulaRequest Request)
    : IRequest<OperationResult<ManufacturingVUFormulaWriteResultDto>>;
