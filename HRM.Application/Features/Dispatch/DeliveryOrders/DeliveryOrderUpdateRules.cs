using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.UpdateDeliveryOrder;
using HRM.Domain.Entities.DeliverySchema;

namespace HRM.Application.Features.Dispatch.DeliveryOrders;

internal static class DeliveryOrderUpdateRules
{
    public static bool HasSameContent(
        DeliveryOrder deliveryOrder,
        UpdateDeliveryOrderCommand request,
        IReadOnlyList<NormalizedDeliveryOrderLine> requestedLines,
        IReadOnlyCollection<Guid> delivererIds)
    {
        if (!string.Equals(deliveryOrder.Receiver, TrimToNull(request.Receiver), StringComparison.Ordinal) ||
            !string.Equals(deliveryOrder.DeliveryAddress, TrimToNull(request.DeliveryAddress), StringComparison.Ordinal) ||
            !string.Equals(deliveryOrder.PaymentType, TrimToNull(request.PaymentType), StringComparison.Ordinal) ||
            !string.Equals(deliveryOrder.PaymentDeadline, TrimToNull(request.PaymentDeadline), StringComparison.Ordinal) ||
            !string.Equals(deliveryOrder.TaxNumber, TrimToNull(request.TaxNumber), StringComparison.Ordinal) ||
            !string.Equals(deliveryOrder.PhoneSnapshot, TrimToNull(request.PhoneSnapshot), StringComparison.Ordinal) ||
            !string.Equals(deliveryOrder.Note, TrimToNull(request.Note), StringComparison.Ordinal) ||
            deliveryOrder.DeliveryPrice != request.DeliveryPrice ||
            deliveryOrder.RequiresUnloading != request.RequiresUnloading)
        {
            return false;
        }

        var existingDelivererIds = deliveryOrder.Deliverers
            .Select(x => x.DelivererInforId)
            .ToHashSet();
        if (!existingDelivererIds.SetEquals(delivererIds))
        {
            return false;
        }

        var activeDetails = deliveryOrder.Details
            .Where(x => x.IsActive && !x.IsAttach)
            .ToArray();
        if (activeDetails.Length != requestedLines.Count)
        {
            return false;
        }

        foreach (var requestedLine in requestedLines)
        {
            var matchingLines = activeDetails
                .Where(x => x.MerchandiseOrderDetailId == requestedLine.MerchandiseOrderDetailId)
                .Take(2)
                .ToArray();
            if (matchingLines.Length != 1)
            {
                return false;
            }

            var existingLine = matchingLines[0];
            if (existingLine.Quantity != requestedLine.Quantity ||
                existingLine.NumOfBags != requestedLine.NumOfBags ||
                !string.Equals(existingLine.LotNoList, requestedLine.LotNoList, StringComparison.Ordinal))
            {
                return false;
            }

            var existingLots = existingLine.LotConsumptions
                .Where(x => x.IsActive)
                .GroupBy(x => x.LotNo, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(x => x.Quantity),
                    StringComparer.OrdinalIgnoreCase);

            if (existingLots.Count != requestedLine.Lots.Count ||
                requestedLine.Lots.Any(lot =>
                    !existingLots.TryGetValue(lot.LotNo, out var quantity) ||
                    quantity != lot.Quantity))
            {
                return false;
            }
        }

        return true;
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
