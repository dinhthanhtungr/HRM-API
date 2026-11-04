using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.PLPU
{
    public class PLPUPagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } // List of items
        public int TotalCount { get; set; } // Total number of items
        public int Page { get; set; } // Current page number
        public int PageSize { get; set; } // Number of items per page
        public int TotalPages { get; set; }// Total number of pages
        public PLPUPagedResult(IEnumerable<T> items, int totalCount, int page, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        }
    }
}
