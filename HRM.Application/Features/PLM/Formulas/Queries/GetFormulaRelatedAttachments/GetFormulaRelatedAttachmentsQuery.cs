using HRM.Application.Features.PLM.Formulas.Dtos.RelatedAttachments;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaRelatedAttachments;

public sealed class GetFormulaRelatedAttachmentsQuery : IRequest<FormulaRelatedAttachmentsDto?>
{
    public Guid FormulaId { get; init; }
}
