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
public class SubscriptionController : ControllerBase
{
    private readonly AppDbContext _context;

    public SubscriptionController(
        AppDbContext context)
    {
        _context = context;
    }

    // ================================
    // GET SUBSCRIPTIONS
    // ================================

    [HttpGet]
    [Authorize(Roles = "Master,Superadmin")]
    public async Task<ActionResult> GetAll()
    {
        // Admin видит все подписки.
        if (User.IsInRole("Superadmin"))
        {
            var allSubscriptions =
                await _context.Subscriptions
                    .AsNoTracking()
                    .Include(s => s.Payments)
                    .Include(s => s.Master)
                    .ToListAsync();

            return Ok(allSubscriptions);
        }

        var email =
            User.FindFirstValue(
                JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        // Master видит только свои подписки.
        var subscriptions =
            await _context.Subscriptions
                .AsNoTracking()

                .Include(s => s.Payments)

                .Include(s => s.Master)
                    .ThenInclude(m => m.User)

                .Where(s =>
                    s.Master.User.Email == email)

                .ToListAsync();

        return Ok(subscriptions);
    }

    // ================================
    // GET SUBSCRIPTION BY ID
    // ================================

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Master,Superadmin")]
    public async Task<ActionResult> GetById(
        int id)
    {
        var sub =
            await _context.Subscriptions

                .AsNoTracking()

                .Include(s => s.Payments)

                .Include(s => s.Master)
                    .ThenInclude(m => m.User)

                .FirstOrDefaultAsync(
                    s => s.Id == id);

        if (sub == null)
            return NotFound(
                "Subscription Not Found.");

        if (User.IsInRole("Superadmin"))
            return Ok(sub);

        var email =
            User.FindFirstValue(
                JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        if (!string.Equals(
                sub.Master.User.Email,
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        return Ok(sub);
    }

    // ================================
    // DELETE
    // ================================

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<ActionResult> DeleteById(
        int id)
    {
        var sub =
            await _context.Subscriptions
                .FirstOrDefaultAsync(
                    s => s.Id == id);

        if (sub == null)
            return NotFound(
                "Subscription Not Found.");

        _context.Subscriptions.Remove(sub);

        await _context.SaveChangesAsync();

        return Ok(
            "Subscription is deleted");
    }

    // ================================
    // CREATE MANUALLY
    // ADMIN ONLY
    // ================================

    [HttpPost]
    [Authorize(Roles = "Superadmin")]
    public async Task<ActionResult> Create(
        [FromBody] CreateSubscriptionDTO dto)
    {
        var sub = new Subscription
        {
            MasterId = dto.MasterId,
            Plan = dto.Plan,
            Status = dto.Status,
            ExpiresAt = dto.ExpiresAt
        };

        _context.Subscriptions.Add(sub);

        await _context.SaveChangesAsync();

        return Ok(sub);
    }

    // ================================
    // UPDATE MANUALLY
    // ADMIN ONLY
    // ================================

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<ActionResult> Update(
        int id,
        [FromBody] CreateSubscriptionDTO dto)
    {
        var subToChange =
            await _context.Subscriptions
                .FirstOrDefaultAsync(
                    s => s.Id == id);

        if (subToChange == null)
            return NotFound(
                "Subscription Not Found.");

        subToChange.MasterId =
            dto.MasterId;

        subToChange.Plan =
            dto.Plan;

        subToChange.Status =
            dto.Status;

        subToChange.ExpiresAt =
            dto.ExpiresAt;

        await _context.SaveChangesAsync();

        return Ok(subToChange);
    }
}