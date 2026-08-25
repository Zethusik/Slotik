using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.Models;

namespace Slotik.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NotificationController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificationController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var notifications = await _context.Set<Notification>().ToListAsync();
        return Ok(notifications);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var notification = await _context.Set<Notification>().FindAsync(id);
        if (notification == null) return NotFound("Message not found");
        return Ok(notification);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Notification notification)
    {
        _context.Set<Notification>().Add(notification);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = notification.Id }, notification);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Notification updated)
    {
        var notification = await _context.Set<Notification>().FindAsync(id);
        if (notification == null) return NotFound("Message not found");

        notification.Type = updated.Type;
        notification.Text = updated.Text;
        notification.IsRead = updated.IsRead;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var notification = await _context.Set<Notification>().FindAsync(id);
        if (notification == null) return NotFound("Message not found");

        _context.Set<Notification>().Remove(notification);
        await _context.SaveChangesAsync();
        return Ok("Message deleted");
    }
}