using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

/// <summary>Tính thống kê Excel theo category và trạng thái QC: số mã distinct, tổng kg và tỷ lệ làm tròn theo nguồn.</summary>
public static class IqcReportCalculator
{
    public static List<QCInputCategoryReportRow> BuildCategoryReport(List<GetSummaryQCInput> reportRows)
    {
        var rows = reportRows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.CategoryName) ? "KHÁC" : x.CategoryName.Trim().ToUpper())
            .Select(g => new QCInputCategoryReportRow
            {
                CategoryName = g.Key,
                ItemCount = g.Select(x => x.ExternalId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count(),
                QtyKg = g.Sum(x => x.QtyKg)
            })
            .OrderByDescending(x => x.ItemCount)
            .ToList();

        var total = rows.Sum(x => x.ItemCount);

        foreach (var row in rows)
        {
            row.Percent = total == 0
                ? 0
                : Math.Round(row.ItemCount * 100m / total, 1);
        }

        return rows;
    }

    public static List<QCInputQcStatusReportRow> BuildQcStatusReport(List<GetSummaryQCInput> reportRows)
    {
        var qcTypes = new[]
        {
            VoucherDetailType.QCPass,
            VoucherDetailType.QCFail,
            VoucherDetailType.Waiter,
            VoucherDetailType.Special
        };

        var raw = reportRows
            .Where(x => qcTypes.Contains(x.VoucherType))
            .GroupBy(x => x.VoucherType)
            .Select(g => new QCInputQcStatusReportRow
            {
                StatusName = MapQcStatusName(g.Key),
                ItemCount = g.Select(x => x.ExternalId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count(),
                QtyKg = g.Sum(x => x.QtyKg)
            })
            .OrderByDescending(x => x.ItemCount)
            .ToList();

        var total = raw.Sum(x => x.ItemCount);

        foreach (var row in raw)
        {
            row.Percent = total == 0
                ? 0
                : Math.Round(row.ItemCount * 100m / total, 1);
        }

        return new List<QCInputQcStatusReportRow>
        {
            MergeQcRow("HÀNG ĐẠT", raw),
            MergeQcRow("HÀNG LỖI", raw),
            MergeQcRow("CHỜ QC", raw),
            MergeQcRow("CHẤP NHẬN ĐẶC BIỆT", raw)
        };
    }

    private static QCInputQcStatusReportRow MergeQcRow(string statusName, List<QCInputQcStatusReportRow> source)
    {
        var row = source.FirstOrDefault(x => x.StatusName == statusName);
        return row ?? new QCInputQcStatusReportRow
        {
            StatusName = statusName,
            ItemCount = 0,
            QtyKg = 0,
            Percent = 0
        };
    }

    private static string MapQcStatusName(VoucherDetailType type) => type switch
    {
        VoucherDetailType.QCPass => "HÀNG ĐẠT",
        VoucherDetailType.QCFail => "HÀNG LỖI",
        VoucherDetailType.Waiter => "CHỜ QC",
        VoucherDetailType.Special => "CHẤP NHẬN ĐẶC BIỆT",
        _ => "KHÁC"
    };

}
