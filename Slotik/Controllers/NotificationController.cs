using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Services;
namespace Slotik.Controllers;
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class NotificationController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (User.UserId() is not int userId) return Unauthorized();
        return Ok(await context.Notifications.Where(n => n.UserId == userId)
            .Select(n => new NotificationResponse(n.Id, n.Type, n.Text, n.IsRead, n.CreatedAt)).ToListAsync(ct));
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        if (User.UserId() is not int userId) return Unauthorized();
        var notification = await context.Notifications.Where(n => n.Id == id && n.UserId == userId)
            .Select(n => new NotificationResponse(n.Id, n.Type, n.Text, n.IsRead, n.CreatedAt)).SingleOrDefaultAsync(ct);
        return notification == null ? NotFound() : Ok(notification);
    }
    [HttpPost]
    public async Task<IActionResult> Create(CreateNotificationDto dto, CancellationToken ct)
    {
        if (User.UserId() is not int userId) return Unauthorized();
        var notification = new Notification { UserId = userId, Type = dto.Type, Text = dto.Text };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = notification.Id },
            new NotificationResponse(notification.Id, notification.Type, notification.Text, false, notification.CreatedAt));
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateNotificationDto dto, CancellationToken ct)
    {
        if (User.UserId() is not int userId) return Unauthorized();
        var notification = await context.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (notification == null) return NotFound();
        notification.IsRead = dto.IsRead;
        await context.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (User.UserId() is not int userId) return Unauthorized();
        var notification = await context.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (notification == null) return NotFound();
        context.Notifications.Remove(notification);
        await context.SaveChangesAsync(ct);
        return NoContent();
    }
}
