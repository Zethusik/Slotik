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

    public BookingController(AppDbContext context)
    {
        _context = context;
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
    public async Task<IActionResult> Create(
        [FromBody] CreateBookingDto dto,
        CancellationToken cancellationToken)
    {
        // JWT stores the email in the "sub" claim.
        // Depending on JWT claim mapping it may appear
        // as ClaimTypes.NameIdentifier.
        var email =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized(new
            {
                error = "invalid_token",
                message = "User email was not found in the token."
            });
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Email == email,
                cancellationToken);

        if (user == null)
        {
            return Unauthorized(new
            {
                error = "user_not_found",
                message = "Authenticated user was not found."
            });
        }

        var service = await _context.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == dto.ServiceId,
                cancellationToken);

        if (service == null)
        {
            return NotFound(new
            {
                error = "service_not_found",
                message = "Service was not found."
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

        // Store booking timestamps in UTC.
        var startsAt = dto.StartsAt.ToUniversalTime();

        if (startsAt <= DateTimeOffset.UtcNow)
        {
            return BadRequest(new
            {
                error = "booking_in_past",
                message = "Booking start time must be in the future."
            });
        }

        var endsAt =
            startsAt.AddMinutes(service.DurationMin);

        // Lock all booking operations for this master
        // inside this transaction.
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({service.MasterId});",
            cancellationToken);

        // Read master after obtaining the lock so
        // SlotStepMin and blocked status are current.
        var master = await _context.Masters
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.Id == service.MasterId,
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
            return Conflict(new
            {
                error = "master_blocked",
                message = "This master is currently unavailable."
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

        var timeZone = GetKyivTimeZone();

        var localStart =
            TimeZoneInfo.ConvertTime(
                startsAt,
                timeZone);

        var localEnd =
            TimeZoneInfo.ConvertTime(
                endsAt,
                timeZone);

        var bookingDate =
            DateOnly.FromDateTime(
                localStart.DateTime);

        var bookingEndDate =
            DateOnly.FromDateTime(
                localEnd.DateTime);

        // ========================================
        // DAY OFF / VACATION CHECK
        // ========================================

        var isDayOff = await _context.DaysOff
            .AsNoTracking()
            .AnyAsync(
                d =>
                    d.MasterId == master.Id &&
                    d.DateFrom <= bookingDate &&
                    d.DateTo >= bookingDate,
                cancellationToken);

        if (isDayOff)
        {
            return Conflict(new
            {
                error = "slot_unavailable",
                message = "The master is unavailable on this date."
            });
        }

        // ========================================
        // WORKING SCHEDULE CHECK
        // ========================================

        var weekday =
            bookingDate.DayOfWeek == DayOfWeek.Sunday
                ? 7
                : (int)bookingDate.DayOfWeek;

        var schedule = await _context.Schedules
            .AsNoTracking()
            .Include(s => s.Intervals)
            .FirstOrDefaultAsync(
                s =>
                    s.MasterId == master.Id &&
                    s.Weekday == weekday,
                cancellationToken);

        if (schedule == null ||
            !schedule.IsWorking ||
            schedule.Intervals.Count == 0)
        {
            return Conflict(new
            {
                error = "slot_unavailable",
                message = "The master is not working at this time."
            });
        }

        // A service cannot continue into another day.
        if (bookingDate != bookingEndDate)
        {
            return Conflict(new
            {
                error = "slot_unavailable",
                message = "The service does not fit inside the working day."
            });
        }

        var localStartTime =
            TimeOnly.FromDateTime(
                localStart.DateTime);

        var localEndTime =
            TimeOnly.FromDateTime(
                localEnd.DateTime);

        var matchingInterval =
            schedule.Intervals
                .FirstOrDefault(i =>
                    localStartTime.CompareTo(i.StartTime) >= 0 &&
                    localEndTime.CompareTo(i.EndTime) <= 0);

        if (matchingInterval == null)
        {
            return Conflict(new
            {
                error = "slot_unavailable",
                message = "The selected time is outside the master's working hours."
            });
        }

        // ========================================
        // SLOT STEP CHECK
        // ========================================

        var ticksFromIntervalStart =
            localStartTime.Ticks -
            matchingInterval.StartTime.Ticks;

        var slotStepTicks =
            TimeSpan
                .FromMinutes(master.SlotStepMin)
                .Ticks;

        if (ticksFromIntervalStart < 0 ||
            ticksFromIntervalStart % slotStepTicks != 0)
        {
            return Conflict(new
            {
                error = "invalid_slot_step",
                message = "The selected time does not match the master's booking step."
            });
        }

        // ========================================
        // DOUBLE BOOKING CHECK
        // ========================================

        // The advisory lock above makes requests for the same
        // master wait for each other. After obtaining the lock
        // we check the database again before inserting.
        var overlaps = await _context.Bookings
            .AsNoTracking()
            .AnyAsync(
                b =>
                    b.MasterId == master.Id &&

                    (
                        b.Status == BookingStatus.Pending ||
                        b.Status == BookingStatus.Confirmed
                    ) &&

                    startsAt < b.EndsAt &&
                    endsAt > b.StartsAt,
                cancellationToken);

        if (overlaps)
        {
            return Conflict(new
            {
                error = "slot_already_booked",
                message = "The selected time is no longer available."
            });
        }

        var booking = new Booking
        {
            StartsAt = startsAt,
            EndsAt = endsAt,

            Status = BookingStatus.Pending,

            Comment =
                string.IsNullOrWhiteSpace(dto.Comment)
                    ? null
                    : dto.Comment.Trim(),

            ReminderSent = false,

            ServiceId = service.Id,
            MasterId = master.Id,
            UserId = user.Id
        };

        _context.Bookings.Add(booking);

        await _context.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            new
            {
                booking.Id,
                booking.StartsAt,
                booking.EndsAt,
                booking.Status,
                booking.Comment,
                booking.ReminderSent,
                booking.ServiceId,
                booking.MasterId,
                booking.UserId
            });
    }

    // ========================================
    // ADMIN: UPDATE BOOKING
    // ========================================

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateBookingDto dto,
        CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings
            .FindAsync(
                new object[] { id },
                cancellationToken);

        if (booking == null)
        {
            return NotFound(new
            {
                error = "booking_not_found",
                message = "Booking was not found."
            });
        }

        var startsAt =
            dto.StartsAt.ToUniversalTime();

        var endsAt =
            dto.EndsAt.ToUniversalTime();

        if (endsAt <= startsAt)
        {
            return BadRequest(new
            {
                error = "invalid_booking_time",
                message = "Booking end time must be after start time."
            });
        }

        booking.StartsAt = startsAt;
        booking.EndsAt = endsAt;
        booking.Status = dto.Status;
        booking.Comment = dto.Comment;
        booking.ReminderSent = dto.ReminderSent;
        booking.ServiceId = dto.ServiceId;
        booking.MasterId = dto.MasterId;
        booking.UserId = dto.UserId;

        await _context.SaveChangesAsync(
            cancellationToken);

        return NoContent();
    }

    // ========================================
    // ADMIN: DELETE BOOKING
    // ========================================

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings
            .FindAsync(
                new object[] { id },
                cancellationToken);

        if (booking == null)
        {
            return NotFound(new
            {
                error = "booking_not_found",
                message = "Booking was not found."
            });
        }

        _context.Bookings.Remove(booking);

        await _context.SaveChangesAsync(
            cancellationToken);

        return NoContent();
    }

    // ========================================
    // AUTH HELPERS
    // ========================================

    private string? GetCurrentEmail()
    {
        return
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue(ClaimTypes.Email) ??
            User.FindFirstValue("email");
    }


    private async Task<User?> GetCurrentUserAsync(
        CancellationToken cancellationToken)
    {
        var email = GetCurrentEmail();

        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Email == email,
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