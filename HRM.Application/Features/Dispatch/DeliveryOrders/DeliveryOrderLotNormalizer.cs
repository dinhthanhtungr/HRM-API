using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Domain.Enums.WareHouses;

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

            if (requestedLots.Any(x => x?.NumOfBags.HasValue == true))
            {
                if (requestedLots.Any(x => x is null || !x.NumOfBags.HasValue || x.NumOfBags < 0) || requestedLots.Sum(x => (long)(x.NumOfBags ?? 0)) != line.NumOfBags)
                {
                    error = "Số bao phải gửi cho tất cả lot và tổng số bao lot phải bằng numOfBags của dòng.";
                    return false;
                }
            }

            var lots = new List<NormalizedDeliveryLot>(requestedLots.Count);
            foreach (var lot in requestedLots)
            {
                // Kiểm tra loại tồn trước khi gộp mã lot để hàng lỗi không bị tính vào hàng đạt.
                if (lot?.StockType is not null &&
                    !string.Equals(lot.StockType, nameof(StockType.FinishedGood), StringComparison.Ordinal) &&
                    !string.Equals(lot.StockType, nameof(StockType.DefectiveFinishedGood), StringComparison.Ordinal))
                {
                    error = "Loại tồn giao hàng phải là FinishedGood hoặc DefectiveFinishedGood.";
                    return false;
                }

                var lotNo = TrimToNull(lot?.LotNo);
                if (lotNo is null || lot!.Quantity <= 0m || lot.NumOfBags < 0)
                {
                    error = "Lot giao hàng phải có lotNo và quantity lớn hơn 0.";
                    return false;
                }

                var existingIndex = lots.FindIndex(x =>
                    string.Equals(x.LotNo, lotNo, StringComparison.OrdinalIgnoreCase) &&
                    x.StockType == (lot.StockType == nameof(StockType.DefectiveFinishedGood) ? StockType.DefectiveFinishedGood : StockType.FinishedGood));

                if (existingIndex >= 0)
                {
                    var existing = lots[existingIndex];
                    lots[existingIndex] = existing with { Quantity = existing.Quantity + lot.Quantity, NumOfBags = (existing.NumOfBags ?? 0) + (lot.NumOfBags ?? 0) };
                }
                else
                {
                    lots.Add(new NormalizedDeliveryLot(lotNo, lot.Quantity,
                        lot.StockType == nameof(StockType.DefectiveFinishedGood) ? StockType.DefectiveFinishedGood : StockType.FinishedGood, lot.NumOfBags));
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

        if (preparedLines.GroupBy(x => x.MerchandiseOrderDetailId).Any(g =>
            g.Any(line => line.Lots.Any(lot => lot.NumOfBags.HasValue)) &&
            g.Any(line => line.Lots.All(lot => !lot.NumOfBags.HasValue))))
        {
            error = "Các dòng trùng PO phải thống nhất gửi số bao từng lot hoặc dùng contract cũ.";
            return false;
        }
        if (preparedLines.GroupBy(x => x.MerchandiseOrderDetailId).Any(g => g.Sum(x => (long)x.NumOfBags) > int.MaxValue))
        {
            error = "Tổng số bao vượt giới hạn cho phép.";
            return false;
        }
        normalizedLines = preparedLines
            .GroupBy(x => x.MerchandiseOrderDetailId)
            .Select(group =>
            {
                var lots = group
                    .SelectMany(x => x.Lots)
                    .GroupBy(x => new { LotNo = DeliveryOrderLotInventoryRules.Normalize(x.LotNo), x.StockType })
                    .Select(lotGroup => new NormalizedDeliveryLot(
                        lotGroup.First().LotNo,
                        lotGroup.Sum(x => x.Quantity), lotGroup.Key.StockType,
                        lotGroup.Any(x => x.NumOfBags.HasValue) ? lotGroup.Sum(x => x.NumOfBags ?? 0) : null))
                    .ToArray();

                return new NormalizedDeliveryOrderLine(
                    group.Key,
                    lots.Sum(x => x.Quantity),
                    group.Sum(x => x.NumOfBags),
                    string.Join(", ", lots.Select(x => x.LotNo).Distinct(StringComparer.OrdinalIgnoreCase)),
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

internal sealed record NormalizedDeliveryLot(string LotNo, decimal Quantity, StockType StockType = StockType.FinishedGood, int? NumOfBags = null);
