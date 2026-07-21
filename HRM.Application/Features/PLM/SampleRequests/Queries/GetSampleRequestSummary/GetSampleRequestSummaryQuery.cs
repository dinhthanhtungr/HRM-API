using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Summary;
using HRM.Domain.Enums.Products;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary
{
    public sealed class GetSampleRequestSummaryQuery
        : PaginationQuery, IRequest<PagedResult<SampleRequestSummaryDto>>
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public Guid? CompanyId { get; set; }
        public Guid? ProductId { get; set; }
        public string? Color { get; set; }
        public string? AdditiveCode { get; set; }
        public SampleRequestStatus? Status { get; set; }
    }
}
