
namespace HRM.Application.Features.Reports.PLPU
{
    public class PLPUPurchaseOverviewQuery : HRM.Application.Commons.Searching.ISmartSearchRequest
    {
        public string? SearchSelections { get; set; }
        public string? SuggestionKeyword { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public Guid? CompanyId { get; set; }
        public Guid? PurchaseOrderId { get; set; }
        public Guid? SupplierId { get; set; }
        public Guid? MaterialId { get; set; }
        public string? OrderType { get; set; }
        public string? Status { get; set; }
        public string? Keyword { get; set; }
        public bool IncludeInactive { get; set; } = false;
        public bool OnlyNeedAction { get; set; } = false;
    }
}
