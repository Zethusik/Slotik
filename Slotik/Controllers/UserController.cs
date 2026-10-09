using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Services;
namespace Slotik.Controllers;
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UserController(AppDbContext context) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (User.UserId() is not int userId) return Unauthorized();
        var user = await context.Users.Where(u => u.Id == userId).Select(ApiResponses.User).SingleOrDefaultAsync(ct);
        return user == null ? NotFound() : Ok(user);
    }
    [HttpPut("me")]
    public Task<IActionResult> UpdateMe(UpdateUserProfileDto dto, CancellationToken ct) =>
        User.UserId() is int userId ? UpdateProfile(userId, dto, ct) : Task.FromResult<IActionResult>(Unauthorized());
    [HttpDelete("me")]
    public Task<IActionResult> DeleteMe(CancellationToken ct) =>
        User.UserId() is int userId ? DeleteUser(userId, ct) : Task.FromResult<IActionResult>(Unauthorized());
    [HttpGet("admin")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> AdminList(CancellationToken ct) =>
        Ok(await context.Users.Select(ApiResponses.User).ToListAsync(ct));
    [HttpGet("admin/{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> AdminGet(int id, CancellationToken ct)
    {
        var user = await context.Users.Where(u => u.Id == id).Select(ApiResponses.User).SingleOrDefaultAsync(ct);
        return user == null ? NotFound() : Ok(user);
    }
    [HttpPut("admin/{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public Task<IActionResult> AdminUpdate(int id, UpdateUserProfileDto dto, CancellationToken ct) => UpdateProfile(id, dto, ct);
    [HttpDelete("admin/{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public Task<IActionResult> AdminDelete(int id, CancellationToken ct) => DeleteUser(id, ct);
    private async Task<IActionResult> UpdateProfile(int id, UpdateUserProfileDto dto, CancellationToken ct)
    {
        var user = await context.Users.FindAsync(new object[] { id }, ct);
        if (user == null) return NotFound();
        user.FirstName = dto.FirstName.Trim(); user.LastName = dto.LastName.Trim(); user.Phone = dto.Phone.Trim();
        await context.SaveChangesAsync(ct);
        return Ok(await context.Users.Where(u => u.Id == id).Select(ApiResponses.User).SingleAsync(ct));
    }
    private async Task<IActionResult> DeleteUser(int id, CancellationToken ct)
    {
        if (await context.EntitlementGrants.AnyAsync(g => context.Masters.Any(m => m.Id == g.MasterId && m.UserId == id), ct))
            return Conflict(new { error = "billing_history_retention", message = "Account deletion requires billing-history retention." });
        var user = await context.Users.FindAsync(new object[] { id }, ct);
        if (user == null) return NotFound();
        context.Users.Remove(user);
        await context.SaveChangesAsync(ct);
        return NoContent();
    }
}
