using Slotik.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;

namespace Slotik.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ScheduleController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IMasterSubscriptionLock _masterLock;

    public ScheduleController(AppDbContext context, IMasterSubscriptionLock masterLock)
    {
        _context = context;
        _masterLock = masterLock;
    }

    // ========================================
    // GET MY WEEKLY SCHEDULE
    // ========================================

    [HttpGet("me")]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> GetMySchedule(
        CancellationToken cancellationToken)
    {
        var master = await GetCurrentMasterAsync(
            cancellationToken);

        if (master == null)
            return NotFound("Master not found.");

        var schedules =
            await _context.Schedules
                .AsNoTracking()
                .Where(s =>
                    s.MasterId == master.Id)
                .Include(s => s.Intervals)
                .OrderBy(s => s.Weekday)
                .ToListAsync(cancellationToken);

        var days =
            Enumerable.Range(1, 7)
                .Select(weekday =>
                {
                    var schedule =
                        schedules.FirstOrDefault(
                            s => s.Weekday == weekday);

                    return new
                    {
                        weekday,

                        isWorking =
                            schedule?.IsWorking
                            ?? false,

                        intervals =
                            schedule?.Intervals
                                .OrderBy(i => i.StartTime)
                                .Select(i => (object)new
                                {
                                    i.Id,
                                    i.StartTime,
                                    i.EndTime
                                })
                                .ToList()
                            ??
                            new List<object>()
                    };
                })
                .ToList();

        return Ok(new
        {
            masterId = master.Id,

            slotStepMin =
                master.SlotStepMin,

            days
        });
    }

    // ========================================
    // UPDATE ONE WEEKDAY
    // ========================================

    [HttpPut("me/day/{weekday:int}")]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> UpdateMyDay(
        int weekday,
        [FromBody] UpdateScheduleDayDto dto,
        CancellationToken cancellationToken)
    {
        if (weekday < 1 || weekday > 7)
        {
            return BadRequest(new
            {
                error = "invalid_weekday",

                message =
                    "Weekday must be between 1 and 7."
            });
        }

        var validationError =
            ValidateIntervals(dto);

        if (validationError != null)
        {
            return BadRequest(new
            {
                error = "invalid_intervals",
                message = validationError
            });
        }

        var master =
            await GetCurrentMasterAsync(
                cancellationToken);

        if (master == null)
            return NotFound("Master not found.");

        await using var transaction = await _masterLock.AcquireAsync(master.Id, cancellationToken);
        await _context.Entry(master).ReloadAsync(cancellationToken);
        if (await BookingMutationGuard.ScheduleConflictsAsync(_context, master, weekday, dto, cancellationToken))
            return Conflict(BookingMutationGuard.Conflict("schedule"));

        var schedule =
            await _context.Schedules
                .Include(s => s.Intervals)
                .FirstOrDefaultAsync(
                    s =>
                        s.MasterId == master.Id &&
                        s.Weekday == weekday,
                    cancellationToken);

        if (schedule == null)
        {
            schedule = new Schedule
            {
                MasterId = master.Id,
                Weekday = weekday,
                IsWorking = dto.IsWorking
            };

            _context.Schedules.Add(schedule);
        }
        else
        {
            schedule.IsWorking =
                dto.IsWorking;

            if (schedule.Intervals.Count > 0)
            {
                _context.ScheduleIntervals
                    .RemoveRange(
                        schedule.Intervals);

                schedule.Intervals.Clear();
            }
        }

        if (dto.IsWorking)
        {
            foreach (var interval in
                     dto.Intervals
                         .OrderBy(i =>
                             i.StartTime))
            {
                schedule.Intervals.Add(
                    new ScheduleInterval
                    {
                        StartTime =
                            interval.StartTime,

                        EndTime =
                            interval.EndTime
                    });
            }
        }

        await OnboardingService.SaveAndCompleteAsync(_context, master.Id, cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Ok(new
        {
            schedule.Id,
            schedule.Weekday,
            schedule.IsWorking,

            intervals =
                schedule.Intervals
                    .OrderBy(i =>
                        i.StartTime)
                    .Select(i => new
                    {
                        i.Id,
                        i.StartTime,
                        i.EndTime
                    })
        });
    }

    // ========================================
    // UPDATE SLOT STEP
    // ========================================

    [HttpPut("me/slot-step")]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> UpdateSlotStep(
        [FromBody] UpdateSlotStepDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.SlotStepMin < 5 ||
            dto.SlotStepMin > 240)
        {
            return BadRequest(new
            {
                error = "invalid_slot_step",

                message =
                    "Slot step must be between " +
                    "5 and 240 minutes."
            });
        }

        if (dto.SlotStepMin % 5 != 0)
        {
            return BadRequest(new
            {
                error = "invalid_slot_step",

                message =
                    "Slot step must be divisible " +
                    "by 5 minutes."
            });
        }

        var master =
            await GetCurrentMasterAsync(
                cancellationToken);

        if (master == null)
            return NotFound("Master not found.");

        await using var transaction = await _masterLock.AcquireAsync(master.Id, cancellationToken);
        await _context.Entry(master).ReloadAsync(cancellationToken);
        if (master.SlotStepMin != dto.SlotStepMin
            && await BookingMutationGuard.StepConflictsAsync(_context, master.Id, dto.SlotStepMin, cancellationToken))
            return Conflict(BookingMutationGuard.Conflict("slot step"));
        master.SlotStepMin =
            dto.SlotStepMin;

        await _context.SaveChangesAsync(
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            master.Id,
            master.SlotStepMin
        });
    }

    // ========================================
    // HELPERS
    // ========================================

    private async Task<Master?>
        GetCurrentMasterAsync(
            CancellationToken cancellationToken)
    {
        var userId = User.UserId();

        if (!userId.HasValue)
            return null;

        return await _context.Masters
            .Include(m => m.User)
            .FirstOrDefaultAsync(
                m => m.UserId == userId.Value,
                cancellationToken);
    }

    private static string?
        ValidateIntervals(
            UpdateScheduleDayDto dto)
    {
        if (!dto.IsWorking)
            return null;

        if (dto.Intervals == null ||
            dto.Intervals.Count == 0)
        {
            return
                "A working day must contain " +
                "at least one working interval.";
        }

        var ordered =
            dto.Intervals
                .OrderBy(i => i.StartTime)
                .ToList();

        foreach (var interval in ordered)
        {
            if (interval.StartTime >=
                interval.EndTime)
            {
                return
                    "Interval start time must " +
                    "be earlier than end time.";
            }
        }

        for (var i = 1;
             i < ordered.Count;
             i++)
        {
            var previous =
                ordered[i - 1];

            var current =
                ordered[i];

            if (current.StartTime <
                previous.EndTime)
            {
                return
                    "Working intervals cannot overlap.";
            }
        }

        return null;
    }
}
