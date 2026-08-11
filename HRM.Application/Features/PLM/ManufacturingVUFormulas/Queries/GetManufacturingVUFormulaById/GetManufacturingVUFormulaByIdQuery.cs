using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.GetManufacturingVUFormulaById;

public sealed record GetManufacturingVUFormulaByIdQuery(Guid ManufacturingVUFormulaId)
    : IRequest<OperationResult<ManufacturingVUFormulaDetailDto>>;
