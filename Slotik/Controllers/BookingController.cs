using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;
using Slotik.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Models.Enums;

namespace Slotik.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BookingController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly BookingPolicy _policy;

    public BookingController(AppDbContext context, IOptions<BookingPolicy> policy)
    {
        _context = context;
        _policy = policy.Value;
    }

    // ========================================
    // CLIENT: MY BOOKINGS
    // ========================================

    [HttpGet("me")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> GetMyBookings(
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(
            cancellationToken);

        if (user == null)
        {
            return Unauthorized(new
            {
                error = "user_not_found",
                message = "Authenticated user was not found."
            });
        }

        var bookings = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.UserId == user.Id)
            .OrderBy(b => b.StartsAt)
            .Select(b => new
            {
                b.Id,
                b.StartsAt,
                b.EndsAt,
                b.Status,
                b.Comment,
                b.ReminderSent,
                b.PendingExpiresAt,

                b.ServiceId,
                serviceName = b.Service.Name,
                durationMin = b.Service.DurationMin,
                price = b.Service.Price,

                b.MasterId
            })
            .ToListAsync(cancellationToken);

        return Ok(bookings);
    }


    // ========================================
    // CLIENT: CANCEL MY BOOKING
    // ========================================

    [HttpPatch("me/{id:int}/cancel")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> CancelMyBooking(
        int id,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(
            cancellationToken);

        if (user == null)
        {
            return Unauthorized(new
            {
                error = "user_not_found",
                message = "Authenticated user was not found."
            });
        }

        var bookingInfo = await _context.Bookings
            .AsNoTracking()
            .Where(b =>
                b.Id == id &&
                b.UserId == user.Id)
            .Select(b => new
            {
                b.MasterId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (bookingInfo == null)
        {
            return NotFound(new
            {
                error = "booking_not_found",
                message = "Booking was not found."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({bookingInfo.MasterId});",
            cancellationToken);

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(
                b =>
                    b.Id == id &&
                    b.UserId == user.Id,
                cancellationToken);

        if (booking == null)
        {
            return NotFound(new
            {
                error = "booking_not_found",
                message = "Booking was not found."
            });
        }

        if (booking.Status != BookingStatus.Pending &&
            booking.Status != BookingStatus.Confirmed)
        {
            return Conflict(new
            {
                error = "booking_cannot_be_cancelled",
                message =
                    "Only pending or confirmed bookings can be cancelled."
            });
        }

        booking.Status = BookingStatus.Cancelled;

        await _context.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Ok(new
        {
            booking.Id,
            booking.Status
        });
    }


    // ========================================
    // MASTER: MY BOOKINGS
    // ========================================

    [HttpGet("master/me")]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> GetMasterBookings(
        CancellationToken cancellationToken)
    {
        var master = await GetCurrentMasterAsync(
            cancellationToken);

        if (master == null)
        {
            return NotFound(new
            {
                error = "master_profile_not_found",
                message = "Master profile was not found."
            });
        }

        var bookings = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.MasterId == master.Id)
            .OrderBy(b => b.StartsAt)
            .Select(b => new
            {
                b.Id,
                b.StartsAt,
                b.EndsAt,
                b.Status,
                b.Comment,
                b.ReminderSent,
                b.PendingExpiresAt,

                b.ServiceId,
                serviceName = b.Service.Name,
                durationMin = b.Service.DurationMin,
                price = b.Service.Price,

                b.UserId,
                clientFirstName = b.User.FirstName,
                clientLastName = b.User.LastName,
                clientPhone = b.User.Phone,
                clientEmail = b.User.Email
            })
            .ToListAsync(cancellationToken);

        return Ok(bookings);
    }


    // ========================================
    // MASTER: CHANGE BOOKING STATUS
    // ========================================

    [HttpPatch("master/{id:int}/status")]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> ChangeBookingStatus(
        int id,
        [FromBody] ChangeBookingStatusDto dto,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(
                typeof(BookingStatus),
                dto.Status))
        {
            return BadRequest(new
            {
                error = "invalid_status",
                message = "Unknown booking status."
            });
        }

        var master = await GetCurrentMasterAsync(
            cancellationToken);

        if (master == null)
        {
            return NotFound(new
            {
                error = "master_profile_not_found",
                message = "Master profile was not found."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({master.Id});",
            cancellationToken);

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(
                b =>
                    b.Id == id &&
                    b.MasterId == master.Id,
                cancellationToken);

        if (booking == null)
        {
            return NotFound(new
            {
                error = "booking_not_found",
                message = "Booking was not found."
            });
        }

        if (booking.Status == BookingStatus.Pending && (booking.PendingExpiresAt ?? booking.StartsAt) <= DateTimeOffset.UtcNow)
        {
            booking.Status = BookingStatus.Cancelled;
            booking.PendingExpiresAt = null;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Conflict(new { error = "pending_booking_expired" });
        }
        if (dto.Status == BookingStatus.Confirmed)
        {
            var error = await BookingRules.ValidateSlotAsync(_context, master.Id, booking.ServiceId,
                booking.StartsAt, booking.EndsAt, booking.Id, _policy, cancellationToken);
            if (error != null) return Conflict(new { error });
        }
        // Repeating the current status is safe.
        if (booking.Status == dto.Status)
        {
            return Ok(new
            {
                booking.Id,
                booking.Status
            });
        }

        var transitionAllowed =
            booking.Status switch
            {
                BookingStatus.Pending =>
                    dto.Status == BookingStatus.Confirmed ||
                    dto.Status == BookingStatus.Cancelled,

                BookingStatus.Confirmed =>
                    dto.Status == BookingStatus.Completed ||
                    dto.Status == BookingStatus.Cancelled,

                _ => false
            };

        if (!transitionAllowed)
        {
            return Conflict(new
            {
                error = "invalid_status_transition",
                message =
                    $"Cannot change booking status from {booking.Status} to {dto.Status}."
            });
        }

        booking.Status = dto.Status;
        if (dto.Status != BookingStatus.Pending) booking.PendingExpiresAt = null;

        await _context.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Ok(new
        {
            booking.Id,
            booking.Status
        });
    }

    // ========================================
    // ADMIN: ALL BOOKINGS
    // ========================================

    [HttpGet]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Service)
            .Include(b => b.Master)
            .Include(b => b.User)
            .ToListAsync(cancellationToken);

        return Ok(bookings);
    }

    // ========================================
    // AVAILABLE BOOKING SLOTS
    // ========================================

    [HttpGet("available-slots")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableSlots(
        [FromQuery] int masterId,
        [FromQuery] int serviceId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, GetKyivTimeZone()).DateTime);
        if (date < today || date > today.AddDays(_policy.MaxAdvanceDays))
            return BadRequest(new { error = "booking_outside_horizon" });
        var service = await _context.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s =>
                    s.Id == serviceId &&
                    s.MasterId == masterId,
                cancellationToken);

        if (service == null)
        {
            return NotFound(new
            {
                error = "service_not_found",
                message = "Service was not found for this master."
            });
        }

        var master = await _context.Masters
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.Id == masterId,
                cancellationToken);

        if (master == null)
        {
            return NotFound(new
            {
                error = "master_not_found",
                message = "Master was not found."
            });
        }

        if (master.IsBlocked)
        {
            return Ok(new
            {
                masterId,
                serviceId,
                date,
                durationMin = service.DurationMin,
                slotStepMin = master.SlotStepMin,
                slots = Array.Empty<AvailableSlotDto>()
            });
        }

        if (service.DurationMin <= 0)
        {
            return BadRequest(new
            {
                error = "invalid_service_duration",
                message = "Service duration must be greater than zero."
            });
        }

        if (master.SlotStepMin <= 0)
        {
            return BadRequest(new
            {
                error = "invalid_slot_step",
                message = "Master slot step must be greater than zero."
            });
        }

        // ========================================
        // DAY OFF / VACATION
        // ========================================

        var isDayOff = await _context.DaysOff
            .AsNoTracking()
            .AnyAsync(
                d =>
                    d.MasterId == masterId &&
                    d.DateFrom <= date &&
                    d.DateTo >= date,
                cancellationToken);

        if (isDayOff)
        {
            return Ok(new
            {
                masterId,
                serviceId,
                date,
                durationMin = service.DurationMin,
                slotStepMin = master.SlotStepMin,
                slots = Array.Empty<AvailableSlotDto>()
            });
        }

        // Schedule uses:
        // 1 = Monday
        // ...
        // 7 = Sunday
        var weekday =
            date.DayOfWeek == DayOfWeek.Sunday
                ? 7
                : (int)date.DayOfWeek;

        var schedule = await _context.Schedules
            .AsNoTracking()
            .Include(s => s.Intervals)
            .FirstOrDefaultAsync(
                s =>
                    s.MasterId == masterId &&
                    s.Weekday == weekday,
                cancellationToken);

        if (schedule == null ||
            !schedule.IsWorking ||
            schedule.Intervals.Count == 0)
        {
            return Ok(new
            {
                masterId,
                serviceId,
                date,
                durationMin = service.DurationMin,
                slotStepMin = master.SlotStepMin,
                slots = Array.Empty<AvailableSlotDto>()
            });
        }

        var timeZone = GetKyivTimeZone();

        var dayStartLocal = date.ToDateTime(
            TimeOnly.MinValue,
            DateTimeKind.Unspecified);

        var nextDayLocal = date
            .AddDays(1)
            .ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Unspecified);

        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            dayStartLocal,
            timeZone);

        var nextDayUtc = TimeZoneInfo.ConvertTimeToUtc(
            nextDayLocal,
            timeZone);

        // Only Pending and Confirmed bookings block time.
        var busyBookings = await _context.Bookings
            .AsNoTracking()
            .Where(BookingRules.ReservesSlot(DateTimeOffset.UtcNow))
            .Where(b =>
                b.MasterId == masterId &&

                (
                    b.Status == BookingStatus.Pending ||
                    b.Status == BookingStatus.Confirmed
                ) &&

                b.StartsAt <
                new DateTimeOffset(
                    nextDayUtc,
                    TimeSpan.Zero) &&

                b.EndsAt >
                new DateTimeOffset(
                    dayStartUtc,
                    TimeSpan.Zero))
            .Select(b => new
            {
                b.StartsAt,
                b.EndsAt
            })
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        var slots = new List<AvailableSlotDto>();

        foreach (var interval in
                 schedule.Intervals.OrderBy(i => i.StartTime))
        {
            var intervalStart = CreateKyivDateTimeOffset(
                date,
                interval.StartTime,
                timeZone);

            var intervalEnd = CreateKyivDateTimeOffset(
                date,
                interval.EndTime,
                timeZone);

            var candidateStart = intervalStart;

            while (candidateStart < intervalEnd)
            {
                var candidateEnd =
                    candidateStart.AddMinutes(
                        service.DurationMin);

                // Service must fully fit inside the interval.
                if (candidateEnd > intervalEnd)
                {
                    break;
                }

                // Do not return slots that already passed.
                if (candidateStart > now)
                {
                    var overlaps =
                        busyBookings.Any(b =>
                            candidateStart < b.EndsAt &&
                            candidateEnd > b.StartsAt);

                    if (!overlaps)
                    {
                        slots.Add(
                            new AvailableSlotDto
                            {
                                StartsAt = candidateStart,
                                EndsAt = candidateEnd
                            });
                    }
                }

                candidateStart =
                    candidateStart.AddMinutes(
                        master.SlotStepMin);
            }
        }

        return Ok(new
        {
            masterId,
            serviceId,
            date,
            durationMin = service.DurationMin,
            slotStepMin = master.SlotStepMin,
            slots
        });
    }

    // ========================================
    // ADMIN: BOOKING BY ID
    // ========================================

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Service)
            .Include(b => b.Master)
            .Include(b => b.User)
            .FirstOrDefaultAsync(
                b => b.Id == id,
                cancellationToken);

        if (booking == null)
        {
            return NotFound(new
            {
                error = "booking_not_found",
                message = "Booking was not found."
            });
        }

        return Ok(booking);
    }

    // ========================================
    // CLIENT: CREATE BOOKING
    // ========================================

    [HttpPost]
    [Authorize(Roles = "Client")]
    [EnableRateLimiting("booking")]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto, CancellationToken cancellationToken)
    {
        if (User.UserId() is not int userId) return Unauthorized();
        if (!await _context.Users.AnyAsync(u => u.Id == userId, cancellationToken)) return Unauthorized();
        var masterId = await _context.Services.Where(s => s.Id == dto.ServiceId)
            .Select(s => (int?)s.MasterId).SingleOrDefaultAsync(cancellationToken);
        if (masterId == null) return NotFound(new { error = "service_not_found" });
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        // User quota lock precedes the master lock; this also serializes requests across different masters.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(-1, {userId})", cancellationToken);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({masterId.Value})", cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var pendingCount = await _context.Bookings.Where(BookingRules.ReservesSlot(now))
            .CountAsync(b => b.UserId == userId && b.Status == BookingStatus.Pending, cancellationToken);
        if (pendingCount >= _policy.MaxPendingPerUser)
            return Conflict(new { error = "pending_booking_limit", limit = _policy.MaxPendingPerUser });
        var service = await _context.Services.AsNoTracking().SingleOrDefaultAsync(s => s.Id == dto.ServiceId, cancellationToken);
        if (service == null || service.MasterId != masterId.Value) return Conflict(new { error = "service_changed" });
        var startsAt = dto.StartsAt.ToUniversalTime();
        if (service.DurationMin <= 0 || service.DurationMin > (DateTimeOffset.MaxValue - startsAt).TotalMinutes)
            return BadRequest(new { error = "invalid_service_duration" });
        var endsAt = startsAt.AddMinutes(service.DurationMin);
        var error = await BookingRules.ValidateSlotAsync(_context, masterId.Value, service.Id,
            startsAt, endsAt, null, _policy, cancellationToken);
        if (error != null) return Conflict(new { error });
        var booking = new Booking
        {
            StartsAt = startsAt, EndsAt = endsAt, Status = BookingStatus.Pending,
            PendingExpiresAt = _policy.PendingDeadline(now, startsAt),
            Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim(),
            UserId = userId, MasterId = masterId.Value, ServiceId = service.Id
        };
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return StatusCode(201, new { booking.Id, booking.StartsAt, booking.EndsAt, booking.Status,
            booking.PendingExpiresAt, booking.Comment, booking.ReminderSent, booking.UserId, booking.MasterId, booking.ServiceId });
    }
    // ========================================
    // ADMIN: UPDATE BOOKING
    // ========================================

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBookingDto dto, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(dto.Status)) return BadRequest(new { error = "invalid_status" });
        var snapshot = await _context.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (snapshot == null) return NotFound();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        foreach (var userId in new[] { snapshot.UserId, dto.UserId }.Distinct().Order())
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(-1, {userId})", cancellationToken);
        foreach (var masterId in new[] { snapshot.MasterId, dto.MasterId }.Distinct().Order())
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({masterId})", cancellationToken);
        var booking = await _context.Bookings.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (booking == null) return NotFound();
        if (booking.MasterId != snapshot.MasterId || booking.UserId != snapshot.UserId)
            return Conflict(new { error = "booking_changed_retry" });
        var start = dto.StartsAt.ToUniversalTime(); var end = dto.EndsAt.ToUniversalTime();
        if (end <= start) return BadRequest(new { error = "invalid_booking_time" });
        if (!BookingRules.CanTransition(booking.Status, dto.Status))
            return Conflict(new { error = "invalid_status_transition" });
        if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled &&
            (start != booking.StartsAt || end != booking.EndsAt || dto.MasterId != booking.MasterId ||
             dto.ServiceId != booking.ServiceId || dto.UserId != booking.UserId))
            return Conflict(new { error = "terminal_booking_immutable" });
        if (!await _context.Services.AnyAsync(s => s.Id == dto.ServiceId && s.MasterId == dto.MasterId, cancellationToken))
            return BadRequest(new { error = "service_master_mismatch" });
        if (!await _context.Users.AnyAsync(u => u.Id == dto.UserId && u.Role == UserRole.Client, cancellationToken))
            return BadRequest(new { error = "client_not_found" });
        var now = DateTimeOffset.UtcNow;
        if (dto.Status is BookingStatus.Pending or BookingStatus.Confirmed)
        {
            if (booking.Status == BookingStatus.Pending && (booking.PendingExpiresAt ?? booking.StartsAt) <= now)
                return Conflict(new { error = "pending_booking_expired" });
            var error = await BookingRules.ValidateSlotAsync(_context, dto.MasterId, dto.ServiceId,
                start, end, id, _policy, cancellationToken);
            if (error != null) return Conflict(new { error });
            if (dto.Status == BookingStatus.Pending &&
                await _context.Bookings.Where(BookingRules.ReservesSlot(now)).CountAsync(b => b.Id != id &&
                    b.UserId == dto.UserId && b.Status == BookingStatus.Pending, cancellationToken) >= _policy.MaxPendingPerUser)
                return Conflict(new { error = "pending_booking_limit" });
        }
        booking.StartsAt = start; booking.EndsAt = end; booking.Status = dto.Status;
        booking.Comment = dto.Comment; booking.ReminderSent = dto.ReminderSent;
        booking.ServiceId = dto.ServiceId; booking.MasterId = dto.MasterId; booking.UserId = dto.UserId;
        if (dto.Status != BookingStatus.Pending) booking.PendingExpiresAt = null;
        else if (booking.PendingExpiresAt == null || start < booking.PendingExpiresAt)
            booking.PendingExpiresAt = _policy.PendingDeadline(now, start);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var snapshot = await _context.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (snapshot == null) return NotFound();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({snapshot.MasterId})", cancellationToken);
        var booking = await _context.Bookings.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (booking == null) return NotFound();
        if (booking.MasterId != snapshot.MasterId) return Conflict(new { error = "booking_changed_retry" });
        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }
    // ========================================
    // AUTH HELPERS
    // ========================================




    private async Task<User?> GetCurrentUserAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.UserId();

        if (!userId.HasValue)
        {
            return null;
        }

        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == userId.Value,
                cancellationToken);
    }


    private async Task<Master?> GetCurrentMasterAsync(
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(
            cancellationToken);

        if (user == null)
        {
            return null;
        }

        return await _context.Masters
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.UserId == user.Id,
                cancellationToken);
    }

    // ========================================
    // TIME ZONE HELPERS
    // ========================================

    private static TimeZoneInfo GetKyivTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Europe/Kyiv");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "FLE Standard Time");
        }
    }

    private static DateTimeOffset CreateKyivDateTimeOffset(
        DateOnly date,
        TimeOnly time,
        TimeZoneInfo timeZone)
    {
        var localDateTime =
            date.ToDateTime(
                time,
                DateTimeKind.Unspecified);

        var offset =
            timeZone.GetUtcOffset(
                localDateTime);

        return new DateTimeOffset(
            localDateTime,
            offset);
    }
}