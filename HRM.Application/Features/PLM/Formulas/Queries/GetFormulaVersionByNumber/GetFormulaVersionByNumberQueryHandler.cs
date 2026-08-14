using HRM.Application.Features.PLM.Formulas.Dtos.Versions;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulaVersions;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaVersionByNumber;

internal sealed class GetFormulaVersionByNumberQueryHandler
    : IRequestHandler<GetFormulaVersionByNumberQuery, FormulaVersionDto?>
{
    private readonly ISender _sender;

    public GetFormulaVersionByNumberQueryHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<FormulaVersionDto?> Handle(
        GetFormulaVersionByNumberQuery request,
        CancellationToken cancellationToken)
    {
        if (request.VersionNo <= 0)
        {
            return null;
        }

        var versions = await _sender.Send(
            new GetFormulaVersionsQuery(request.FormulaId),
            cancellationToken);

        return versions?.FirstOrDefault(x => x.VersionNo == request.VersionNo);
    }
}
