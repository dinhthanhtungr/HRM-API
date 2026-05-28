using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Commons.Pagination
{
    public class PaginationQuery
    {
        private const int DefaultPageNumber = 1;
        private const int DefaultPageSize = 15;
        private const int MaxPageSize = 100;

        public int PageNumber { get; init; } = DefaultPageNumber;
        public int PageSize { get; init; } = DefaultPageSize;

        public string? Keyword { get; init; }

        public string? SortBy { get; init; }
        public string? SortDirection { get; init; }

        public int NormalizedPageNumber => PageNumber < 1 ? DefaultPageNumber : PageNumber;

        public int NormalizedPageSize
        {
            get
            {
                if (PageSize < 1) return DefaultPageSize;
                return PageSize > MaxPageSize ? MaxPageSize : PageSize;
            }
        }

        public string? NormalizedKeyword =>
            string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();

        public string? NormalizedSortBy =>
            string.IsNullOrWhiteSpace(SortBy) ? null : SortBy.Trim();

        public bool SortDescending =>
            string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(SortDirection, "descending", StringComparison.OrdinalIgnoreCase);
    }
}
