using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.GetManufacturingVUFormulaPrefill;

public sealed record GetManufacturingVUFormulaPrefillQuery(Guid FormulaId)
    : IRequest<OperationResult<ManufacturingVUFormulaPrefillDto>>;
