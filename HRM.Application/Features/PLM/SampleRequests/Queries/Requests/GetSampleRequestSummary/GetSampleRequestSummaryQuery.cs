using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Summary;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary
{
    public sealed class GetSampleRequestSummaryQuery
        : SampleRequestSummaryFilters, IRequest<PagedResult<SampleRequestSummaryDto>>
    {
    }
}
