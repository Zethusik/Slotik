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
public class DaysOffController : ControllerBase
{
    private readonly AppDbContext _context;

    public DaysOffController(AppDbContext context)
    {
        _context = context;
    }

    // ========================================
    // ADMIN: ALL DAYS OFF
    // ========================================

    [HttpGet]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var daysOff = await _context.DaysOff
            .AsNoTracking()
            .OrderBy(d => d.MasterId)
            .ThenBy(d => d.DateFrom)
            .Select(d => new
            {
                d.Id,
                d.DateFrom,
                d.DateTo,
                d.MasterId
            })
            .ToListAsync(cancellationToken);

        return Ok(daysOff);
    }

    // ========================================
    // ADMIN: DAY OFF BY ID
    // ========================================

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var dayOff = await _context.DaysOff
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new
            {
                d.Id,
                d.DateFrom,
                d.DateTo,
                d.MasterId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dayOff == null)
        {
            return NotFound(new
            {
                error = "day_off_not_found",
                message = "Day off was not found."
            });
        }

        return Ok(dayOff);
    }

    // ========================================
    // MASTER: MY DAYS OFF
    // ========================================

    [HttpGet("me")]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> GetMine(
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

        var daysOff = await _context.DaysOff
            .AsNoTracking()
            .Where(d => d.MasterId == master.Id)
            .OrderBy(d => d.DateFrom)
            .Select(d => new
            {
                d.Id,
                d.DateFrom,
                d.DateTo
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            masterId = master.Id,
            daysOff
        });
    }

    // ========================================
    // MASTER: MY DAY OFF BY ID
    // ========================================

    [HttpGet("me/{id:int}")]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> GetMineById(
        int id,
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

        var dayOff = await _context.DaysOff
            .AsNoTracking()
            .Where(d =>
                d.Id == id &&
                d.MasterId == master.Id)
            .Select(d => new
            {
                d.Id,
                d.DateFrom,
                d.DateTo
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dayOff == null)
        {
            return NotFound(new
            {
                error = "day_off_not_found",
                message = "Day off was not found."
            });
        }

        return Ok(dayOff);
    }

    // ========================================
    // MASTER: CREATE DAY OFF
    // ========================================

    [HttpPost("me")]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> CreateMine(
        [FromBody] CreateDaysOffDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.DateFrom > dto.DateTo)
        {
            return BadRequest(new
            {
                error = "invalid_date_range",
                message = "DateFrom must be before or equal to DateTo."
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

        // Prevent simultaneous vacation changes
        // for the same master.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({master.Id});",
            cancellationToken);

        var overlapsExistingDayOff =
            await _context.DaysOff
                .AsNoTracking()
                .AnyAsync(
                    d =>
                        d.MasterId == master.Id &&
                        d.DateFrom <= dto.DateTo &&
                        d.DateTo >= dto.DateFrom,
                    cancellationToken);

        if (overlapsExistingDayOff)
        {
            return Conflict(new
            {
                error = "days_off_overlap",
                message = "This date range overlaps an existing day off."
            });
        }

        var hasActiveBookings =
            await HasActiveBookingsAsync(
                master.Id,
                dto.DateFrom,
                dto.DateTo,
                cancellationToken);

        if (hasActiveBookings)
        {
            return Conflict(new
            {
                error = "active_bookings_exist",
                message =
                    "There are active bookings inside this date range."
            });
        }

        var dayOff = new DaysOff
        {
            DateFrom = dto.DateFrom,
            DateTo = dto.DateTo,
            MasterId = master.Id
        };

        _context.DaysOff.Add(dayOff);

        await _context.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            new
            {
                dayOff.Id,
                dayOff.DateFrom,
                dayOff.DateTo,
                dayOff.MasterId
            });
    }

    // ========================================
    // MASTER: UPDATE MY DAY OFF
    // ========================================

    [HttpPut("me/{id:int}")]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> UpdateMine(
        int id,
        [FromBody] CreateDaysOffDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.DateFrom > dto.DateTo)
        {
            return BadRequest(new
            {
                error = "invalid_date_range",
                message = "DateFrom must be before or equal to DateTo."
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

        var dayOff = await _context.DaysOff
            .FirstOrDefaultAsync(
                d =>
                    d.Id == id &&
                    d.MasterId == master.Id,
                cancellationToken);

        if (dayOff == null)
        {
            return NotFound(new
            {
                error = "day_off_not_found",
                message = "Day off was not found."
            });
        }

        var overlapsExistingDayOff =
            await _context.DaysOff
                .AsNoTracking()
                .AnyAsync(
                    d =>
                        d.MasterId == master.Id &&
                        d.Id != id &&
                        d.DateFrom <= dto.DateTo &&
                        d.DateTo >= dto.DateFrom,
                    cancellationToken);

        if (overlapsExistingDayOff)
        {
            return Conflict(new
            {
                error = "days_off_overlap",
                message = "This date range overlaps an existing day off."
            });
        }

        var hasActiveBookings =
            await HasActiveBookingsAsync(
                master.Id,
                dto.DateFrom,
                dto.DateTo,
                cancellationToken);

        if (hasActiveBookings)
        {
            return Conflict(new
            {
                error = "active_bookings_exist",
                message =
                    "There are active bookings inside this date range."
            });
        }

        dayOff.DateFrom = dto.DateFrom;
        dayOff.DateTo = dto.DateTo;

        await _context.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return NoContent();
    }

    // ========================================
    // MASTER: DELETE MY DAY OFF
    // ========================================

    [HttpDelete("me/{id:int}")]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> DeleteMine(
        int id,
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

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({master.Id});",
            cancellationToken);

        var dayOff = await _context.DaysOff
            .FirstOrDefaultAsync(
                d =>
                    d.Id == id &&
                    d.MasterId == master.Id,
                cancellationToken);

        if (dayOff == null)
        {
            return NotFound(new
            {
                error = "day_off_not_found",
                message = "Day off was not found."
            });
        }

        _context.DaysOff.Remove(dayOff);

        await _context.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return NoContent();
    }

    // ========================================
    // HELPERS
    // ========================================

    private async Task<Master?> GetCurrentMasterAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.UserId();

        if (!userId.HasValue)
        {
            return null;
        }

        return await _context.Masters
            .AsNoTracking()
            .Include(m => m.User)
            .FirstOrDefaultAsync(
                m => m.UserId == userId.Value,
                cancellationToken);
    }

    private async Task<bool> HasActiveBookingsAsync(
        int masterId,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken cancellationToken)
    {
        var timeZone = GetKyivTimeZone();

        var rangeStartLocal =
            dateFrom.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Unspecified);

        var rangeEndLocal =
            dateTo
                .AddDays(1)
                .ToDateTime(
                    TimeOnly.MinValue,
                    DateTimeKind.Unspecified);

        var rangeStartUtcDateTime =
            TimeZoneInfo.ConvertTimeToUtc(
                rangeStartLocal,
                timeZone);

        var rangeEndUtcDateTime =
            TimeZoneInfo.ConvertTimeToUtc(
                rangeEndLocal,
                timeZone);

        var rangeStartUtc =
            new DateTimeOffset(
                rangeStartUtcDateTime,
                TimeSpan.Zero);

        var rangeEndUtc =
            new DateTimeOffset(
                rangeEndUtcDateTime,
                TimeSpan.Zero);

        return await _context.Bookings
            .AsNoTracking()
            .Where(BookingRules.ReservesSlot(DateTimeOffset.UtcNow))
            .AnyAsync(
                b =>
                    b.MasterId == masterId &&

                    (
                        b.Status == BookingStatus.Pending ||
                        b.Status == BookingStatus.Confirmed
                    ) &&

                    b.StartsAt < rangeEndUtc &&
                    b.EndsAt > rangeStartUtc,
                cancellationToken);
    }

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
}