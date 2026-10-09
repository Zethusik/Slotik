using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;

namespace Slotik.Services;

public static class BookingMutationGuard
{
    public static object Conflict(string field) => new
    {
        error = "active_bookings_conflict",
        message = $"Cannot change {field} while future active bookings conflict with the change. Cancel the conflicting bookings first."
    };

    // Caller owns the same master transaction lock as all booking writers.
    public static IQueryable<Booking> FutureActive(AppDbContext db, int masterId, DateTimeOffset now) =>
        db.Bookings.AsNoTracking().Where(BookingRules.ReservesSlot(now))
            .Where(b => b.MasterId == masterId && b.StartsAt > now);

    public static async Task<bool> ScheduleConflictsAsync(AppDbContext db, Master master, int weekday,
        UpdateScheduleDayDto proposed, CancellationToken ct)
    {
        var bookings = await FutureActive(db, master.Id, DateTimeOffset.UtcNow).ToListAsync(ct);
        foreach (var booking in bookings)
        {
            var (day, start, end, sameDate) = LocalTimes(booking);
            if (day != weekday) continue;
            if (!proposed.IsWorking || !sameDate || !Fits(start, end, master.SlotStepMin,
                proposed.Intervals.Select(i => (i.StartTime, i.EndTime)))) return true;
        }
        return false;
    }

    public static async Task<bool> StepConflictsAsync(AppDbContext db, int masterId, int proposedStep, CancellationToken ct)
    {
        var bookings = await FutureActive(db, masterId, DateTimeOffset.UtcNow).ToListAsync(ct);
        if (bookings.Count == 0) return false;
        var schedules = await db.Schedules.AsNoTracking().Include(s => s.Intervals)
            .Where(s => s.MasterId == masterId).ToListAsync(ct);
        foreach (var booking in bookings)
        {
            var (day, start, end, sameDate) = LocalTimes(booking);
            var schedule = schedules.SingleOrDefault(s => s.Weekday == day);
            if (!sameDate || schedule?.IsWorking != true || !Fits(start, end, proposedStep,
                schedule.Intervals.Select(i => (i.StartTime, i.EndTime)))) return true;
        }
        return false;
    }

    public static Task<bool> CategoryConflictsAsync(AppDbContext db, int masterId, int categoryId, CancellationToken ct) =>
        db.Services.AnyAsync(s => s.MasterId == masterId && s.GroupId != null
            && (s.Group == null || s.Group.CategoryId != categoryId), ct);

    public static string? NormalizeAddress(string? address) => string.IsNullOrWhiteSpace(address) ? null : address.Trim();

    public static bool LocationChanges(Master master, int districtId, string? address, double? latitude, double? longitude) =>
        master.DistrictId != districtId || NormalizeAddress(master.Address) != NormalizeAddress(address)
        || master.Latitude != latitude || master.Longitude != longitude;

    private static (int Day, TimeOnly Start, TimeOnly End, bool SameDate) LocalTimes(Booking booking)
    {
        var zone = BookingRules.KyivTimeZone();
        var start = TimeZoneInfo.ConvertTime(booking.StartsAt, zone).DateTime;
        var end = TimeZoneInfo.ConvertTime(booking.EndsAt, zone).DateTime;
        return (start.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)start.DayOfWeek,
            TimeOnly.FromDateTime(start), TimeOnly.FromDateTime(end), start.Date == end.Date && end > start);
    }

    private static bool Fits(TimeOnly start, TimeOnly end, int step, IEnumerable<(TimeOnly Start, TimeOnly End)> intervals) =>
        step > 0 && intervals.Any(i => start >= i.Start && end <= i.End
            && (start.Ticks - i.Start.Ticks) % TimeSpan.FromMinutes(step).Ticks == 0);
}
