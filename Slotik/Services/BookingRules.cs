using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.Models;
using Slotik.Models.Enums;

namespace Slotik.Services;

public static class BookingRules
{
    // Expired Pending requests never reserve slots or consume the pending quota.
    public static Expression<Func<Booking, bool>> ReservesSlot(DateTimeOffset now) => b =>
        b.Status == BookingStatus.Confirmed || (b.Status == BookingStatus.Pending
            && (b.PendingExpiresAt == null ? b.StartsAt > now : b.PendingExpiresAt > now));

    public static bool CanTransition(BookingStatus from, BookingStatus to) => Enum.IsDefined(to) &&
        (from == to || (from == BookingStatus.Pending && to is BookingStatus.Confirmed or BookingStatus.Cancelled)
            || (from == BookingStatus.Confirmed && to is BookingStatus.Completed or BookingStatus.Cancelled));

    public static async Task<string?> ValidateSlotAsync(AppDbContext db, int masterId, int serviceId,
        DateTimeOffset start, DateTimeOffset end, int? excludeId, BookingPolicy policy, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (end <= start) return "invalid_booking_time";
        if (start <= now || start > now.AddDays(policy.MaxAdvanceDays)) return "booking_outside_horizon";
        var service = await db.Services.AsNoTracking().SingleOrDefaultAsync(s => s.Id == serviceId, ct);
        if (service == null || service.MasterId != masterId) return "service_master_mismatch";
        if (service.DurationMin <= 0 || (end - start).TotalMinutes != service.DurationMin) return "invalid_service_duration";
        var master = await db.Masters.AsNoTracking().SingleOrDefaultAsync(m => m.Id == masterId, ct);
        if (master == null || master.IsBlocked || master.SlotStepMin <= 0) return "master_unavailable";
        var zone = KyivTimeZone();
        var localStart = TimeZoneInfo.ConvertTime(start, zone); var localEnd = TimeZoneInfo.ConvertTime(end, zone);
        var date = DateOnly.FromDateTime(localStart.DateTime);
        if (date != DateOnly.FromDateTime(localEnd.DateTime)) return "slot_unavailable";
        if (await db.DaysOff.AnyAsync(d => d.MasterId == masterId && d.DateFrom <= date && d.DateTo >= date, ct))
            return "slot_unavailable";
        var weekday = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        var schedule = await db.Schedules.AsNoTracking().Include(s => s.Intervals)
            .SingleOrDefaultAsync(s => s.MasterId == masterId && s.Weekday == weekday, ct);
        if (schedule?.IsWorking != true) return "slot_unavailable";
        var startTime = TimeOnly.FromDateTime(localStart.DateTime); var endTime = TimeOnly.FromDateTime(localEnd.DateTime);
        var interval = schedule.Intervals.FirstOrDefault(i => startTime >= i.StartTime && endTime <= i.EndTime);
        if (interval == null) return "slot_unavailable";
        if ((startTime.Ticks - interval.StartTime.Ticks) % TimeSpan.FromMinutes(master.SlotStepMin).Ticks != 0)
            return "invalid_slot_step";
        if (await db.Bookings.Where(ReservesSlot(now)).AnyAsync(b => b.MasterId == masterId && b.Id != excludeId
                && start < b.EndsAt && end > b.StartsAt, ct)) return "slot_already_booked";
        return null;
    }

    public static TimeZoneInfo KyivTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time"); }
    }
}
