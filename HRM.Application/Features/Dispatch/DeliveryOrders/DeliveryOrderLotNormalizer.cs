using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

namespace HRM.Application.Features.Dispatch.DeliveryOrders;

internal static class DeliveryOrderLotNormalizer
{
    public static bool TryNormalize(
        IReadOnlyCollection<DeliveryOrderLineRequest>? lines,
        out IReadOnlyList<NormalizedDeliveryOrderLine> normalizedLines,
        out string? error)
    {
        normalizedLines = [];
        error = null;

        if (lines is null || lines.Count == 0)
        {
            error = "Phiếu giao hàng phải có ít nhất một dòng sản phẩm.";
            return false;
        }

        var preparedLines = new List<PreparedLine>(lines.Count);

        foreach (var line in lines)
        {
            if (line is null ||
                line.MerchandiseOrderDetailId == Guid.Empty ||
                line.Quantity <= 0m ||
                line.NumOfBags < 0)
            {
                error = "Dòng giao hàng không hợp lệ.";
                return false;
            }

            IReadOnlyCollection<DeliveryOrderLotRequest> requestedLots;
            if (line.Lots is null)
            {
                var legacyLotNo = TrimToNull(line.LotNoList);
                if (legacyLotNo is null)
                {
                    error = "Mỗi dòng giao hàng phải có ít nhất một lot.";
                    return false;
                }

                requestedLots =
                [
                    new DeliveryOrderLotRequest
                    {
                        LotNo = legacyLotNo,
                        Quantity = line.Quantity
                    }
                ];
            }
            else
            {
                if (line.Lots.Count == 0)
                {
                    error = "Mỗi dòng giao hàng phải có ít nhất một lot.";
                    return false;
                }

                requestedLots = line.Lots;
            }

            var lots = new List<NormalizedDeliveryLot>(requestedLots.Count);
            foreach (var lot in requestedLots)
            {
                var lotNo = TrimToNull(lot?.LotNo);
                if (lotNo is null || lot!.Quantity <= 0m)
                {
                    error = "Lot giao hàng phải có lotNo và quantity lớn hơn 0.";
                    return false;
                }

                var existingIndex = lots.FindIndex(x =>
                    string.Equals(x.LotNo, lotNo, StringComparison.OrdinalIgnoreCase));

                if (existingIndex >= 0)
                {
                    var existing = lots[existingIndex];
                    lots[existingIndex] = existing with { Quantity = existing.Quantity + lot.Quantity };
                }
                else
                {
                    lots.Add(new NormalizedDeliveryLot(lotNo, lot.Quantity));
                }
            }

            var lotQuantity = lots.Sum(x => x.Quantity);
            if (lotQuantity != line.Quantity)
            {
                error = "Quantity của dòng giao hàng phải bằng tổng quantity của Lots.";
                return false;
            }

            preparedLines.Add(new PreparedLine(
                line.MerchandiseOrderDetailId,
                line.NumOfBags,
                lots));
        }

        normalizedLines = preparedLines
            .GroupBy(x => x.MerchandiseOrderDetailId)
            .Select(group =>
            {
                var lots = group
                    .SelectMany(x => x.Lots)
                    .GroupBy(x => x.LotNo, StringComparer.OrdinalIgnoreCase)
                    .Select(lotGroup => new NormalizedDeliveryLot(
                        lotGroup.First().LotNo,
                        lotGroup.Sum(x => x.Quantity)))
                    .ToArray();

                return new NormalizedDeliveryOrderLine(
                    group.Key,
                    lots.Sum(x => x.Quantity),
                    group.Sum(x => x.NumOfBags),
                    string.Join(", ", lots.Select(x => x.LotNo)),
                    lots);
            })
            .ToArray();

        return true;
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record PreparedLine(
        Guid MerchandiseOrderDetailId,
        int NumOfBags,
        IReadOnlyList<NormalizedDeliveryLot> Lots);
}

internal sealed record NormalizedDeliveryOrderLine(
    Guid MerchandiseOrderDetailId,
    decimal Quantity,
    int NumOfBags,
    string LotNoList,
    IReadOnlyList<NormalizedDeliveryLot> Lots);

internal sealed record NormalizedDeliveryLot(string LotNo, decimal Quantity);
