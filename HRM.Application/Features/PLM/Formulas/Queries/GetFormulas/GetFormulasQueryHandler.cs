using HRM.Application.Features.PLM.Formulas.Dtos.GetFormulas;
using HRM.Application.Features.PLM.Formulas.Services;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulas;

internal sealed class GetFormulasQueryHandler
    : IRequestHandler<GetFormulasQuery, FormulaList>
{
    private readonly FormulaListQueryService _formulaListQueryService;

    public GetFormulasQueryHandler(FormulaListQueryService formulaListQueryService)
    {
        _formulaListQueryService = formulaListQueryService;
    }

    public Task<FormulaList> Handle(
        GetFormulasQuery request,
        CancellationToken cancellationToken)
    {
        return _formulaListQueryService.GetAsync(request, cancellationToken: cancellationToken);
    }
}
