using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetManufacturingFormulaMaterials;

public sealed class GetManufacturingFormulaMaterialsQuery : IRequest<FormulaInformationDto?>
{
    public Guid ManufacturingFormulaId { get; set; }
}
