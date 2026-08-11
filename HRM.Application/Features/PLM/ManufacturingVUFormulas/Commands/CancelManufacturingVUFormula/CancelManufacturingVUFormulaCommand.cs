using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Commands.CancelManufacturingVUFormula;

public sealed record CancelManufacturingVUFormulaCommand(Guid ManufacturingVUFormulaId)
    : IRequest<OperationResult<ManufacturingVUFormulaWriteResultDto>>;
