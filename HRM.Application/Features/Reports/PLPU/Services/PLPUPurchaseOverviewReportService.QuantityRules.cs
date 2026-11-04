using HRM.Domain.Enums.Devandqa;
namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
        private static bool IsAutoAcceptPackagingQc(PLPUPurchaseOrderLineRaw line)
        {

            var result =
                line.CategoryId == MaterialPackagingCategoryId
                && line.CreatedDate.HasValue;

            return result;
        }
        private static bool IsDeliveryOtdEvaluable(PLPUPurchaseOrderDetailReportDto detail)
        {
            if (detail.OrderedQuantity <= 0 || !detail.RequestDeliveryDate.HasValue)
                return false;

            return detail.DateWhenReceivedEnough.HasValue;
        }

        private static bool IsAcceptanceOtdEvaluable(PLPUPurchaseOrderDetailReportDto detail)
        {
            if (detail.OrderedQuantity <= 0 || !detail.RequestDeliveryDate.HasValue)
                return false;

            return detail.DateWhenAcceptedEnough.HasValue;
        }

        private static bool IsDeliveryOnTime(PLPUPurchaseOrderDetailReportDto detail)
        {
            return detail.DateWhenReceivedEnough.HasValue &&
                   detail.RequestDeliveryDate.HasValue &&
                   IsBeforeNextDay(detail.DateWhenReceivedEnough.Value, detail.RequestDeliveryDate.Value);
        }

        private static bool IsAcceptanceOnTime(PLPUPurchaseOrderDetailReportDto detail)
        {
            return detail.DateWhenAcceptedEnough.HasValue &&
                   detail.RequestDeliveryDate.HasValue &&
                   IsBeforeNextDay(detail.DateWhenAcceptedEnough.Value, detail.RequestDeliveryDate.Value);
        }

        private static bool IsBeforeNextDay(DateTime actualDate, DateTime dueDate)
        {
            return actualDate < dueDate.Date.AddDays(1);
        }

        private static bool IsOverDelivered(decimal received, decimal ordered)
        {
            if (ordered <= 0)
                return received > 0;

            var toleranceQty = ordered * OverDeliveryTolerancePercent / 100m;

            return received > ordered + toleranceQty;
        }

        private static bool IsCancelledOrInactiveStatus(string? status)
        {
            status ??= string.Empty;

            return status.Equals(StatusCancelled, StringComparison.OrdinalIgnoreCase) ||
                   status.Equals(StatusCanceled, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsClosedStatus(string? status)
        {
            status ??= string.Empty;

            return status.Equals(StatusClosed, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasFlag(string flags, string flag)
        {
            return SplitFlags(flags).Contains(flag, StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> SplitFlags(string flags)
        {
            if (string.IsNullOrWhiteSpace(flags))
                return Enumerable.Empty<string>();

            return flags
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string ReceiptMatchKey(string poExternalId, string materialCode)
        {
            return $"{NormalizeKey(poExternalId)}|{NormalizeKey(materialCode)}";
        }

        private static string NormalizeKey(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static decimal Positive(decimal value)
        {
            return value > 0 ? value : 0;
        }

        private static decimal ResolveUnitPrice(
            decimal orderedQuantity,
            decimal? unitPriceAgreed,
            decimal? totalPriceAgreed,
            decimal? baseCostSnapshot)
        {
            if (unitPriceAgreed.HasValue)
                return unitPriceAgreed.Value;

            if (totalPriceAgreed.HasValue && orderedQuantity > 0)
                return totalPriceAgreed.Value / orderedQuantity;

            return baseCostSnapshot ?? 0;
        }

        private static decimal Percent(decimal numerator, decimal denominator)
        {
            return denominator == 0 ? 0 : Math.Round(numerator / denominator * 100, 2);
        }

        private static decimal Percent(int numerator, int denominator)
        {
            return denominator == 0 ? 0 : Math.Round((decimal)numerator / denominator * 100, 2);
        }

        private static int NormalizePageNumber(int pageNumber)
        {
            return pageNumber <= 0 ? 1 : pageNumber;
        }

        private static int NormalizePageSize(int pageSize)
        {
            return pageSize <= 0 ? 15 : pageSize;
        }

        private static PLPUPagedResult<T> ToPagedResult<T>(List<T> items, int pageNumber, int pageSize)
        {
            var pagedItems = items
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PLPUPagedResult<T>(pagedItems, items.Count, pageNumber, pageSize);
        }


        private static decimal LowerCompletionBound(decimal ordered)
        {
            if (ordered <= 0)
                return 0;

            return ordered * (100m - CompletionTolerancePercent) / 100m;
        }

        private static decimal UpperCompletionBound(decimal ordered)
        {
            if (ordered <= 0)
                return 0;

            return ordered * (100m + CompletionTolerancePercent) / 100m;
        }

        private static bool IsQuantityEnough(decimal actual, decimal ordered)
        {
            if (ordered <= 0)
                return actual <= 0;

            return actual >= LowerCompletionBound(ordered);
        }

        private static decimal RemainingWithTolerance(decimal ordered, decimal actual)
        {
            return IsQuantityEnough(actual, ordered)
                ? 0
                : Positive(ordered - actual);
        }

        private static decimal OverReceivedWithTolerance(decimal received, decimal ordered)
        {
            return IsOverDelivered(received, ordered)
                ? Positive(received - ordered)
                : 0;
        }


}
