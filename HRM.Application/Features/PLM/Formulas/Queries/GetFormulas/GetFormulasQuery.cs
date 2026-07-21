using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Formulas.Dtos.GetFormulas;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulas
{
    public sealed class GetFormulasQuery
        : PaginationQuery, IRequest<FormulaList>
    {
        public Guid? CompanyId { get; set; }
        public string? Status { get; set; }
        public bool IsMerchadiseOrder { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? SampleRequestId { get; set; }
        public Guid? FormulaId { get; set; }
    }
}
