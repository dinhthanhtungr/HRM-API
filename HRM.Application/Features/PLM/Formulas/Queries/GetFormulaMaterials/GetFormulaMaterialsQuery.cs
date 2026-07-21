using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaMaterials;

public sealed class GetFormulaMaterialsQuery : IRequest<FormulaDto>
{
    public Guid FormulaId { get; set; }
}
