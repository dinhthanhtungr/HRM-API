using HRM.Domain.Entities.EnergyScheme;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Enums.Deliveries;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Rules;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;

internal sealed partial class ExecutivePnLActualReader
{
    private IQueryable<MerchandiseOrder> GetReportableMerchandiseOrders()
    {
        return _dbContext.MerchandiseOrders
            .AsNoTracking()
            .Where(x => x.Status != MerchadiseStatus.Cancelled.ToString()
                && x.CustomerExternalIdSnapshot != ExecutivePnLReportRules.InternalCustomerExternalId);
    }

    private IQueryable<MerchandiseOrderDetail> GetReportableMerchandiseOrderDetails()
    {
        return _dbContext.MerchandiseOrderDetails
            .AsNoTracking()
            .Where(x => x.MerchandiseOrder.Status != MerchadiseStatus.Cancelled.ToString()
                && x.MerchandiseOrder.CustomerExternalIdSnapshot != ExecutivePnLReportRules.InternalCustomerExternalId);
    }

    private IQueryable<DeliveryOrder> GetReportableDeliveryOrders()
    {
        return _dbContext.DeliveryOrders
            .AsNoTracking()
            .Where(x => x.Status != DeliveryOrderStatus.Canceled.ToString()
                && x.CustomerExternalIdSnapShot != ExecutivePnLReportRules.InternalCustomerExternalId);
    }

    private IQueryable<DeliveryOrderDetail> GetReportableDeliveryOrderDetails()
    {
        return _dbContext.DeliveryOrderDetails
            .AsNoTracking()
            .Where(x => x.DeliveryOrder.Status != DeliveryOrderStatus.Canceled.ToString()
                && x.DeliveryOrder.CustomerExternalIdSnapShot != ExecutivePnLReportRules.InternalCustomerExternalId);
    }

    private IQueryable<MfgProductionOrder> GetReportableProductionOrders()
    {
        return _dbContext.MfgProductionOrders
            .AsNoTracking()
            .Where(x => x.Status != ManufacturingProductOrder.Canceled.ToString()
                && x.CustomerExternalIdSnapshot != ExecutivePnLReportRules.InternalCustomerExternalId);
    }

    private static bool TryGetActual(
        Dictionary<string, ExecutivePnLMonthlyActual> actuals,
        int year,
        int month,
        out ExecutivePnLMonthlyActual actual)
    {
        return actuals.TryGetValue(ExecutivePnLPeriod.MonthKey(year, month), out actual!);
    }

    private static short ResolveMeterGroupId(
        long meterId,
        DateTime timestamp,
        short fallbackGroupId,
        IReadOnlyList<MeterGroupHistory> histories)
    {
        var history = histories.FirstOrDefault(x =>
            x.MeterId == meterId &&
            x.ValidFrom <= timestamp &&
            (!x.ValidTo.HasValue || timestamp < x.ValidTo.Value));

        return history?.GroupId ?? fallbackGroupId;
    }

    private static int? ResolveTariffId(
        short groupId,
        DateOnly date,
        IReadOnlyList<GroupTariffMap> groupTariffMaps)
    {
        var map = groupTariffMaps.FirstOrDefault(x =>
            x.GroupId == groupId &&
            x.ValidFrom <= date &&
            (!x.ValidTo.HasValue || date < x.ValidTo.Value));

        return map?.TariffId;
    }

    private static TariffVersion? ResolveTariffVersion(
        int tariffId,
        DateOnly date,
        IReadOnlyList<TariffVersion> versions)
    {
        return versions.FirstOrDefault(x =>
            x.TariffId == tariffId &&
            x.ValidFrom <= date &&
            (!x.ValidTo.HasValue || date < x.ValidTo.Value));
    }

    private static TouCalendar? ResolveTouCalendar(
        DateOnly date,
        IReadOnlyList<TouCalendar> calendars)
    {
        return calendars.FirstOrDefault(x =>
            (!x.StartDate.HasValue || x.StartDate.Value <= date) &&
            (!x.EndDate.HasValue || date <= x.EndDate.Value));
    }

    private static string? ResolveTouBand(
        DateTime timestamp,
        TouCalendar? calendar,
        IReadOnlyList<TouWindow> windows,
        IReadOnlyList<TouException> exceptions)
    {
        if (calendar is null)
        {
            return null;
        }

        var theDate = DateOnly.FromDateTime(timestamp);
        var time = timestamp.TimeOfDay;

        var exception = exceptions.FirstOrDefault(x =>
            x.CalendarId == calendar.CalendarId &&
            x.TheDate == theDate &&
            IsWithinTimeRange(time, x.StartTime.TimeOfDay, x.EndTime.TimeOfDay));

        if (!string.IsNullOrWhiteSpace(exception?.Band))
        {
            return exception.Band;
        }

        var window = windows.FirstOrDefault(x =>
            x.CalendarId == calendar.CalendarId &&
            MatchesWeekday(x.Weekday, timestamp.DayOfWeek) &&
            IsWithinTimeRange(time, x.StartTime.TimeOfDay, x.EndTime.TimeOfDay));

        return string.IsNullOrWhiteSpace(window?.Band) ? null : window.Band;
    }

    private static bool MatchesWeekday(short storedWeekday, DayOfWeek dayOfWeek)
    {
        var dow0Based = (short)dayOfWeek;
        var dow1Based = (short)(dayOfWeek == DayOfWeek.Sunday ? 7 : (int)dayOfWeek);

        return storedWeekday == dow0Based || storedWeekday == dow1Based;
    }

    private static bool IsWithinTimeRange(TimeSpan time, TimeSpan start, TimeSpan end)
    {
        if (start <= end)
        {
            return time >= start && time < end;
        }

        return time >= start || time < end;
    }

    private static decimal ResolveBandPrice(
        int versionId,
        string? band,
        IReadOnlyList<TariffBandRate> bandRates)
    {
        if (!string.IsNullOrWhiteSpace(band))
        {
            var matched = bandRates.FirstOrDefault(x =>
                x.VersionId == versionId &&
                string.Equals(x.Band, band, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
            {
                return matched.PriceVndPerKwh;
            }
        }

        var fallback = bandRates.FirstOrDefault(x => x.VersionId == versionId);
        return fallback?.PriceVndPerKwh ?? 0m;
    }

}

