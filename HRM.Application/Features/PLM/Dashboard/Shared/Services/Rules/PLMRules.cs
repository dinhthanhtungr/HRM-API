using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Features.PLM.Dashboard.Shared.Services.Rules
{
    /// <summary>
    /// Centralizes dashboard filtering and status rules so summary, monthly, and drilldown APIs stay consistent.
    /// </summary>
    internal static class PLMRules
    {
        /// <summary>
        /// Internal customer code excluded from revenue and analysis reports.
        /// </summary>
        public const string InternalCustomerExternalId = "KH_VIETAUS";

        public const string SampleRequestsSource = "sampleRequests";
        public const string ProductionOrdersSource = "productionOrders";
        public const string OrdersSource = "orders";
        public const string FinishedCompletion = "finished";
        public const string UnfinishedCompletion = "unfinished";

        public static readonly string[] SampleRequestFinishedStatuses =
        [
            SampleRequestStatus.Completed.ToString(),
            SampleRequestStatus.SampleSent.ToString()
        ];

        public static readonly string[] SampleRequestExcludedStatuses =
        [
            SampleRequestStatus.Cancelled.ToString()
        ];

        public static readonly string[] ProductionOrderFinishedStatuses =
        [
            ManufacturingProductOrder.Finished.ToString(),
            ManufacturingProductOrder.Stocked.ToString()
        ];

        public static readonly string[] ProductionOrderExcludedStatuses =
        [
            ManufacturingProductOrder.Canceled.ToString(),
            "Cancelled"
        ];

        public static readonly string[] OrderFinishedStatuses =
        [
            MerchadiseStatus.Delivered.ToString()
        ];

        public static readonly string[] OrderExcludedStatuses =
        [
            MerchadiseStatus.Cancelled.ToString()
        ];

        /// <summary>
        /// Checks whether a status is cancelled, supporting both Cancelled and Canceled spellings.
        /// </summary>
        public static bool IsCancelledStatus(string? status)
        {
            return string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsProductionOrderSource(string source)
        {
            return source.Equals(ProductionOrdersSource, StringComparison.OrdinalIgnoreCase)
                || source.Equals("manufacturing", StringComparison.OrdinalIgnoreCase)
                || source.Equals("manufacturings", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsOrderSource(string source)
        {
            return source.Equals(OrdersSource, StringComparison.OrdinalIgnoreCase)
                || source.Equals("saleOrders", StringComparison.OrdinalIgnoreCase)
                || source.Equals("merchandiseOrders", StringComparison.OrdinalIgnoreCase)
                || source.Equals("merchadiseOrders", StringComparison.OrdinalIgnoreCase);
        }
    }
}
