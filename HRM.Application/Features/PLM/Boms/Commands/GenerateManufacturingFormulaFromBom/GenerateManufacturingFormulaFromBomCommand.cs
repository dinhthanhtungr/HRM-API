using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.GenerateManufacturingFormulaFromBom;

public sealed record GenerateManufacturingFormulaFromBomCommand(
    Guid BomVersionId,
    GenerateManufacturingFormulaFromBomRequest Request)
    : IRequest<OperationResult<GeneratedManufacturingFormulaDto>>;
