using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
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

        [JsonIgnore]
        public int NormalizedPageNumber => PageNumber < 1 ? DefaultPageNumber : PageNumber;

        [JsonIgnore]
        public int NormalizedPageSize
        {
            get
            {
                if (PageSize < 1) return DefaultPageSize;
                return PageSize > MaxPageSize ? MaxPageSize : PageSize;
            }
        }

        [JsonIgnore]
        public string? NormalizedKeyword =>
            string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();

        [JsonIgnore]
        public string? NormalizedSortBy =>
            string.IsNullOrWhiteSpace(SortBy) ? null : SortBy.Trim();

        [JsonIgnore]
        public bool SortDescending =>
            string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(SortDirection, "descending", StringComparison.OrdinalIgnoreCase);
    }
}
