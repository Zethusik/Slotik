using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Models.Enums;
using Slotik.Services;
using System.Security.Claims;

namespace Slotik.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MasterController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPhotoService _photoService;

    public MasterController(AppDbContext context, IPhotoService photoService)
    {
        _context = context;
        _photoService = photoService;
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
                (m.Category != null && EF.Functions.ILike(m.Category.Name, pattern, @"\")) ||
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
                AvatarUrl = m.User.AvatarUrl.IsNullOrEmpty() ? string.Empty : m.User.AvatarUrl,
                Slug = m.Slug,
                Rating = m.Bookings
                            .Where(b => b.Review != null)
                            .Select(b => (double?)b.Review!.Rating)
                            .Average() ?? null,
                ClientsCount = m.Bookings
                            .Where(b => b.Status == BookingStatus.Completed)
                            .Select(b => b.UserId)
                            .Distinct()
                            .Count(),
                PhotoId = m.User.PhotoId.IsNullOrEmpty() ? string.Empty : m.User.PhotoId,
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
            .Include(m => m.User)
            .Include(m => m.PortfolioPhotos)
            .Include(m=>m.Services)
            .Include(m => m.District)
                .ThenInclude(d => d.City)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (master == null) return NotFound(new { message = "Master not found" });
        return Ok(master);
    }
    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var master = await _context.Masters
            .Include(m => m.User)
            .Include(m => m.Category)
            .Include(m => m.District)
                .ThenInclude(d => d.City)
            .Include(m => m.Services)
            .FirstOrDefaultAsync(m => m.Slug == slug);

        if (master == null) return NotFound(new { message = "Master not found" });

       

        return Ok(new
        {
            master.Id,
            master.UserId,
            master.Slug,
            master.About,
            master.ExperienceYears,
            master.SlotStepMin,
            master.IsBlocked,
            master.CategoryId,
            CategoryName = master.Category?.Name,
            master.DistrictId,
            DistrictName = master.District?.Name,
            CityName = master.District?.City?.Name,
            master.Address,
            master.Latitude,
            master.Longitude,
            User = new
            {
                master.User.Id,
                master.User.FirstName,
                master.User.LastName,
                master.User.Email,
                master.User.Phone,
                master.User.CreatedAt
            },
            master.Services,
            master.User.AvatarUrl,
            master.User.PhotoId
        });
    }

    [HttpPatch("{id:int}/location")]
    [Authorize]
    public async Task<IActionResult> UpdateLocation(int id, [FromBody] UpdateMasterLocationDto dto)
    {
        var master = await _context.Masters
            .Include(m => m.District)
                .ThenInclude(d => d.City)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (master == null) return NotFound(new { message = "Master not found" });

        if (dto.DistrictId.HasValue)
        {
            master.DistrictId = dto.DistrictId.Value;
        }

        master.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
        master.Latitude = dto.Latitude;
        master.Longitude = dto.Longitude;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Location updated successfully",
            districtId = master.DistrictId,
            districtName = master.District?.Name,
            cityName = master.District?.City?.Name,
            address = master.Address,
            latitude = master.Latitude,
            longitude = master.Longitude
        });
    }

    [HttpPost]
    [Authorize(Roles ="Master,Superadmin")]
    public async Task<IActionResult> Create([FromForm] CreateMasterDto dto, [FromQuery] int? id)
    {
        var userid = User.FindFirstValue("userId");

        if (!int.TryParse(userid, out var UserId))
            return Unauthorized();

        var Role = User.FindFirstValue(ClaimTypes.Role);

        if (Role.ToString() != "Master")
        {
            if (id == null) { return BadRequest(new { message = "id is null" }); }
            UserId = id.Value;
        }
            var user = await _context.Users.FindAsync(UserId);


            if (user == null)
            {
                return NotFound(new { message = $"User with Id {UserId} not found" });
            }

        if (user.Role != UserRole.Master)
        {
            return BadRequest(new
            {
                message = "Selected user does not have Master role."
            });
        }










        var exists = await _context.Masters.AnyAsync(m => m.UserId == UserId);
        if (exists)
        {
            return BadRequest(new { message = "Master for this user already exists" });
        }

        List<PortfolioPhoto> photos = new List<PortfolioPhoto>();

        if (dto.portfolioPhotos != null && dto.portfolioPhotos.Any()) {

            if (dto.portfolioPhotos.Count > 10) { return BadRequest(new { message = "No more than 10 photos in portfolio" }); }
            foreach (var file in dto.portfolioPhotos)
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Corrupted file uploaded");

                var result = await _photoService.AddPhotoAsync(file);

                if (result.Error != null)
                {
                    return BadRequest(new
                    {
                        message = result.Error.Message
                    });
                }

                PortfolioPhoto photo = new PortfolioPhoto
                {
                    PhotoId = result.PublicId,
                    PhotoUrl = result.SecureUrl.AbsoluteUri,
                    
                };

                
               photos.Add(photo);

            }
        }

        var master = new Master
        {
            UserId = UserId,
            CategoryId = dto.CategoryId,
            DistrictId = dto.DistrictId,
            Slug = dto.Slug,
            About = dto.About,
            ExperienceYears = dto.ExperienceYears,
            SlotStepMin = dto.SlotStepMin,
            Address = dto.Address,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            PortfolioPhotos = photos
            
        };

        _context.Masters.Add(master);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = master.Id }, master);
    }

    [HttpPut]
    [Authorize(Roles = "Master,Superadmin")]
    public async Task<IActionResult> Update( [FromForm] CreateMasterDto dto, [FromQuery]int? id)
    {
        var userid = User.FindFirstValue("userId");

        if (!int.TryParse(userid, out var UserId))
            return Unauthorized();

        if (User.FindFirstValue(ClaimTypes.Role).ToString() == "Superadmin")
        {
            if (id == null) { return BadRequest(new { message = "id is null" }); }
            UserId = id.Value;
        }

        var master = await _context.Masters
             .Include(m => m.PortfolioPhotos)
             .FirstOrDefaultAsync(m => m.UserId == UserId);

        if (master == null) return NotFound(new { message = "Master not found" });

        List<PortfolioPhoto> photos = new List<PortfolioPhoto>();

        if (dto.portfolioPhotos != null && dto.portfolioPhotos.Any())
        {
            if (dto.portfolioPhotos.Count > 10) { return BadRequest(new { message = "No more than 10 photos in portfolio" }); }

            foreach (var file in dto.portfolioPhotos)
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Corrupted file uploaded");

                var result = await _photoService.AddPhotoAsync(file);

                if (result.Error != null)
                {
                    return BadRequest(new
                    {
                        message = result.Error.Message
                    });
                }

                PortfolioPhoto photo = new PortfolioPhoto
                {
                    PhotoId = result.PublicId,
                    PhotoUrl = result.SecureUrl.AbsoluteUri,

                };


                photos.Add(photo);

                

            }

            foreach (var photo in master.PortfolioPhotos.ToList())
            {
                var remResult =await _photoService.DeletePhotoAsync(photo.PhotoId);

                if (remResult.Error != null) { return BadRequest(new { message = remResult.Error.Message }); }
                _context.PortfolioPhotos.Remove(photo);
                master.PortfolioPhotos.Remove(photo);
            }

            foreach (var photo in photos) { master.PortfolioPhotos.Add(photo); }
        }
        



        master.Slug = dto.Slug;
        master.About = dto.About;
        master.ExperienceYears = dto.ExperienceYears;
        master.SlotStepMin = dto.SlotStepMin;
        master.CategoryId = dto.CategoryId;
        master.DistrictId = dto.DistrictId;
        master.Address = dto.Address;
        master.Latitude = dto.Latitude;
        master.Longitude = dto.Longitude;

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
    public async Task<IActionResult> UpdateMasterSubscription(int id, [FromQuery] SubscriptionPlan? plan, [FromQuery] int? days)
    {
        if (plan == null && days == null) { return BadRequest("Empty values - days, plan"); }
        var m = await _context.Masters.Include(m=>m.Subscriptions).FirstOrDefaultAsync(m=>m.Id==id);
        if (m == null) return NotFound(new { message = "Master not found" });

        var activeSub = m.Subscriptions
                .Where(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > DateTimeOffset.UtcNow && s.Plan != SubscriptionPlan.Free)
                .OrderByDescending(s => s.ExpiresAt)
                .FirstOrDefault();

        Subscription nSub = new Subscription { };

        if (activeSub == null && plan == null) { return BadRequest("Master has an active Free plan."); }

        if (activeSub == null && plan != null)
        {
            

            

            nSub.Plan = plan.Value;

            if (plan != SubscriptionPlan.Free)
            {
                if (days != null)
                {
                    if (days.Value <= 0) { return BadRequest("Wrong days value"); }
                    nSub.ExpiresAt = DateTimeOffset.UtcNow.AddDays(days.Value);
                }
                else
                {
                    nSub.ExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
                }
            }
            

            

            
            nSub.MasterId = m.Id;
            nSub.Status = SubscriptionStatus.Active;
            

            await _context.Subscriptions.AddAsync(nSub);
            await _context.SaveChangesAsync();

            return Ok(new { text = "Ok", Plan = nSub.Plan, ExpiresAt = nSub.ExpiresAt });
        }

        if (plan != null)
        {
            nSub.Plan = plan.Value;

            if (days != null && plan != SubscriptionPlan.Free)
            {
                nSub.ExpiresAt = activeSub.ExpiresAt.AddDays(days.Value);
            }
            else 
            {
                if (plan != SubscriptionPlan.Free)
                {
                    nSub.ExpiresAt = activeSub.ExpiresAt;
                }
               
            }

            
            nSub.MasterId = activeSub.MasterId;
           
            nSub.Status = SubscriptionStatus.Active;

            activeSub.Status = SubscriptionStatus.Cancelled;

            await _context.Subscriptions.AddAsync(nSub);




        }

        if (days != null && plan == null)
        {
            if (days.Value <= 0) { return BadRequest("Wrong days value"); }
            activeSub.ExpiresAt = activeSub.ExpiresAt.AddDays(days.Value);
            await _context.SaveChangesAsync();
            return Ok(new { text = "Ok", Plan = activeSub.Plan, ExpiresAt = activeSub.ExpiresAt });
        }


        await _context.SaveChangesAsync();
        return Ok(new { text = "Ok", Plan = nSub.Plan, ExpiresAt = nSub.ExpiresAt });
    }

    [HttpPost("test-seed/expire-in-10m")]
    [Authorize(Roles = "Superadmin")]
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
