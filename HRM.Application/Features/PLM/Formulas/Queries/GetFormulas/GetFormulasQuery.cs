using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Formulas.Dtos.GetFormulas;
using HRM.Domain.Enums.Merchadises;
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
        /// <summary>
        /// Khách hàng của đơn đang tạo. Khi là KH_VIETAUS, Formula đã Gửi mẫu
        /// hoặc Hoàn thành của mọi khách hàng hợp lệ đều có thể được chọn.
        /// </summary>
        public Guid? CustomerId { get; set; }
        /// <summary>
        /// Loại đơn đang lập. Khi có giá trị hợp lệ, cả bốn loại đơn đều cho phép
        /// Formula gắn Sample Request SampleSent hoặc Completed.
        /// </summary>
        public OrderType? OrderType { get; set; }
        public Guid? SampleRequestId { get; set; }
        public Guid? FormulaId { get; set; }
    }
}
