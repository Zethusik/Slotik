using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;

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

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var bookings = await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.Master)
            .Include(b => b.User)
            .ToListAsync();
        return Ok(bookings);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var booking = await _context.Bookings
            .Include(b => b.Service)
            .Include(b => b.Master)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound("Booking not found... Claude Сode wanted a snack)))");
        return Ok(booking);
    }

    [HttpPost]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto)
    {
        var booking = new Booking
        {
            StartsAt = dto.StartsAt,
            EndsAt = dto.EndsAt,
            Status = dto.Status,
            Comment = dto.Comment,
            ReminderSent = dto.ReminderSent,
            ServiceId = dto.ServiceId,
            MasterId = dto.MasterId,
            UserId = dto.UserId
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateBookingDto dto)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking == null) return NotFound("Booking not found...");

        booking.StartsAt = dto.StartsAt;
        booking.EndsAt = dto.EndsAt;
        booking.Status = dto.Status;
        booking.Comment = dto.Comment;
        booking.ReminderSent = dto.ReminderSent;
        booking.ServiceId = dto.ServiceId;
        booking.MasterId = dto.MasterId;
        booking.UserId = dto.UserId;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking == null) return NotFound("Booking not found...");

        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync();
        return Ok("Booking deleted...");
    }
}