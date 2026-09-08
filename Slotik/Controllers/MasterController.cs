using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Models.Enums;

namespace Slotik.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MasterController : ControllerBase
{
    private readonly AppDbContext _context;

    public MasterController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/Master?status=expired&categoryId=3
    [HttpGet]
    public async Task<IActionResult> GetMasters([FromQuery] string? status, [FromQuery] int? categoryId)
    {
        var now = DateTimeOffset.UtcNow;
        var query = _context.Masters
            .Include(m => m.User)
            .Include(m => m.Category)
            .Include(m => m.District)
                .ThenInclude(d => d.City)
            .Include(m => m.Subscriptions)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(m => m.CategoryId == categoryId.Value);
        }

        var list = await query.Select(m => new MasterAdminDto
        {
            Id = m.Id,
            FirstName = m.User.FirstName,
            LastName = m.User.LastName,
            Category = m.Category.Name,
            City = m.District != null && m.District.City != null ? m.District.City.Name : "Kyiv",
            Status = m.Subscriptions.Any(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > now)
                ? "active"
                : "expired",
            SubscriptionUntil = m.Subscriptions
                .OrderByDescending(s => s.ExpiresAt)
                .Select(s => (DateTimeOffset?)s.ExpiresAt)
                .FirstOrDefault(),
            Tariff = m.Subscriptions
                .OrderByDescending(s => s.ExpiresAt)
                .Select(s => s.Plan.ToString().ToLower())
                .FirstOrDefault() ?? "free"
        }).ToListAsync();

        if (!string.IsNullOrEmpty(status))
        {
            list = list.Where(m => m.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Ok(list);
    }

    // GET /api/Master/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var master = await _context.Masters
            .Include(m => m.User)
            .Include(m => m.Category)
            .Include(m => m.District)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (master == null) return NotFound(new { message = "Master not found" });
        return Ok(master);
    }

    // GET /api/Master/slug/{slug}
    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var master = await _context.Masters
            .Include(m => m.User)
            .Include(m => m.Category)
            .Include(m => m.District)
            .FirstOrDefaultAsync(m => m.Slug == slug);

        if (master == null) return NotFound(new { message = "Master not found" });
        return Ok(master);
    }

    // POST /api/Master
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateMasterDto dto)
    {
        var master = new Master
        {
            UserId = dto.UserId,
            CategoryId = dto.CategoryId,
            DistrictId = dto.DistrictId,
            Slug = dto.Slug,
            About = dto.About,
            ExperienceYears = dto.ExperienceYears,
            SlotStepMin = dto.SlotStepMin,
            IsBlocked = dto.IsBlocked
        };

        _context.Masters.Add(master);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = master.Id }, master);
    }

    // PUT /api/Master/{id}
    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] CreateMasterDto dto)
    {
        var master = await _context.Masters.FindAsync(id);
        if (master == null) return NotFound(new { message = "Master not found" });

        master.Slug = dto.Slug;
        master.About = dto.About;
        master.ExperienceYears = dto.ExperienceYears;
        master.SlotStepMin = dto.SlotStepMin;
        master.IsBlocked = dto.IsBlocked;
        master.CategoryId = dto.CategoryId;
        master.DistrictId = dto.DistrictId;
        master.UserId = dto.UserId;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/Master/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var master = await _context.Masters.FindAsync(id);
        if (master == null) return NotFound(new { message = "Master not found" });

        _context.Masters.Remove(master);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Master deleted successfully" });
    }

    // PATCH /api/Master/{id}/block
    [HttpPatch("{id:int}/block")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> ToggleBlockMaster(int id)
    {
        var master = await _context.Masters.Include(m => m.Subscriptions).FirstOrDefaultAsync(m => m.Id == id);
        if (master == null) return NotFound(new { message = "Master not found" });

        var activeSubs = master.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active).ToList();
        foreach (var sub in activeSubs)
        {
            sub.Status = SubscriptionStatus.Cancelled;
        }

        master.IsBlocked = !master.IsBlocked;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Master block status updated successfully", isBlocked = master.IsBlocked });
    }

    // PATCH /api/Master/{id}/subscription
    [HttpPatch("{id:int}/subscription")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> UpdateMasterSubscription(int id, [FromBody] UpdateSubscriptionDto dto)
    {
        var master = await _context.Masters.FindAsync(id);
        if (master == null) return NotFound(new { message = "Master not found" });

        var newSub = new Models.Subscription
        {
            MasterId = master.Id,
            Plan = dto.Plan,
            Status = SubscriptionStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(dto.Days)
        };

        _context.Subscriptions.Add(newSub);
        await _context.SaveChangesAsync();

        return Ok(newSub);
    }
}