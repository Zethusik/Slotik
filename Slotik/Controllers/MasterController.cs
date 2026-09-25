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

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetMasters(
        [FromQuery] string? status,
        [FromQuery] int? categoryId,
        [FromQuery] int? cityId,
        [FromQuery] int? districtId,
        [FromQuery] string? search)
    {
        var now = DateTimeOffset.UtcNow;
        var query = _context.Masters
            .Include(m => m.User)
            .Include(m => m.Category)
            .Include(m => m.District)
                .ThenInclude(d => d.City)
            .Include(m => m.Subscriptions)
            .Include(m => m.Services)
            .Include(m => m.Bookings)
                .ThenInclude(b => b.Review)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(m => m.CategoryId == categoryId.Value);
        }

        if (cityId.HasValue)
        {
            query = query.Where(m => m.District != null && m.District.CityId == cityId.Value);
        }

        if (districtId.HasValue)
        {
            query = query.Where(m => m.DistrictId == districtId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var escapedTerm = search.Trim()
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");

            var pattern = $"%{escapedTerm}%";

            query = query.Where(m =>
                EF.Functions.ILike(m.User.FirstName, pattern, @"\") ||
                EF.Functions.ILike(m.User.LastName, pattern, @"\") ||
                EF.Functions.ILike(m.User.FirstName + " " + m.User.LastName, pattern, @"\") ||
                EF.Functions.ILike(m.User.LastName + " " + m.User.FirstName, pattern, @"\") ||
                m.Services.Any(s => EF.Functions.ILike(s.Name, pattern, @"\"))
            );
        }

        var mastersList = await query.ToListAsync();

        var list = mastersList.Select(m =>
        {
            var activeSub = m.Subscriptions
                .Where(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > now && s.Plan != SubscriptionPlan.Free)
                .OrderByDescending(s => s.ExpiresAt)
                .FirstOrDefault();

            var isBlocked = m.IsBlocked;
            var currentStatus = isBlocked ? "blocked" : "active";
            var currentTariff = activeSub != null ? activeSub.Plan.ToString().ToLower() : "free";

            return new MasterAdminDto
            {
                Id = m.Id,
                FirstName = m.User.FirstName,
                LastName = m.User.LastName,
                Category = m.Category?.Name ?? string.Empty,
                City = m.District?.City?.Name ?? "Kyiv",
                Status = currentStatus,
                SubscriptionUntil = activeSub != null ? activeSub.ExpiresAt : null,
                Tariff = currentTariff,
                IsBlocked = m.IsBlocked,
                DistrictName = m.District?.Name ?? string.Empty,
                CreatedAt = m.User.CreatedAt,
                AvatarUrl = string.Empty,
                Slug = m.Slug,
                Rating = m.Bookings
                            .Where(b => b.Review != null)
                            .Select(b => (double?)b.Review!.Rating)
                            .Average() ?? null,
                ClientsCount = m.Bookings
                            .Where(b => b.Status == BookingStatus.Completed)
                            .Select(b => b.UserId)
                            .Distinct()
                            .Count()
            };
        }).ToList();

        if (!string.IsNullOrEmpty(status))
        {
            list = list.Where(m => m.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Ok(list);
    }

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

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateMasterDto dto)
    {
        var exists = await _context.Masters.AnyAsync(m => m.UserId == dto.UserId);
        if (exists)
        {
            return BadRequest(new { message = "Master for this user already exists" });
        }

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

    [HttpPatch("{id:int}/block")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> ToggleBlockMaster(int id)
    {
        var master = await _context.Masters.Include(m => m.Subscriptions).FirstOrDefaultAsync(m => m.Id == id);
        if (master == null) return NotFound(new { message = "Master not found" });

        master.IsBlocked = !master.IsBlocked;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Master block status updated successfully", isBlocked = master.IsBlocked });
    }

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

    [HttpPost("test-seed/expire-in-10m")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateTestMasterWith10mSub()
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == "test10m@slotik.com");
        if (user == null)
        {
            user = new User
            {
                FirstName = "Test",
                LastName = "10Min",
                Email = "test10m@slotik.com",
                PasswordHash = "d357150517d3e65ae84985f7b705ad99fdc38372a22ecea0cecaf8aaf820a249",
                Role = UserRole.Master
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        var category = await _context.Categories.FirstOrDefaultAsync() ?? new Category { Name = "Manicure" };
        var district = await _context.Districts.FirstOrDefaultAsync();

        var master = await _context.Masters.Include(m => m.Subscriptions).FirstOrDefaultAsync(m => m.UserId == user.Id);
        if (master == null)
        {
            master = new Master
            {
                UserId = user.Id,
                CategoryId = category.Id,
                DistrictId = district?.Id ?? 1,
                Slug = "test-master-10m",
                ExperienceYears = 5,
                SlotStepMin = 30
            };
            _context.Masters.Add(master);
            await _context.SaveChangesAsync();
        }

        var sub = new Models.Subscription
        {
            MasterId = master.Id,
            Plan = SubscriptionPlan.Pro,
            Status = SubscriptionStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        _context.Subscriptions.Add(sub);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Test master created successfully. Subscription expires in 10 minutes.",
            masterId = master.Id,
            slug = master.Slug,
            tariff = "pro",
            subscriptionUntil = sub.ExpiresAt
        });
    }
}
