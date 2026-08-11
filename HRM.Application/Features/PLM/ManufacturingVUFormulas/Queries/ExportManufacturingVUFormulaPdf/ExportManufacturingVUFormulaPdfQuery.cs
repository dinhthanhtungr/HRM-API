using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.ExportManufacturingVUFormulaPdf;

public sealed record ExportManufacturingVUFormulaPdfQuery(Guid ManufacturingVUFormulaId)
    : IRequest<OperationResult<ManufacturingVUFormulaPdfFileDto>>;
