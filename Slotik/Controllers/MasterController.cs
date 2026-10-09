using Microsoft.AspNetCore.RateLimiting;
using CloudinaryDotNet.Actions;
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
    private readonly IMasterSubscriptionLock _masterLock;
    private readonly IUserWriteLock _userLock;

    public MasterController(AppDbContext context, IPhotoService photoService, IMasterSubscriptionLock masterLock, IUserWriteLock userLock)
    {
        _context = context;
        _photoService = photoService;
        _masterLock = masterLock;
        _userLock = userLock;
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
        var query = _context.Masters.AsNoTracking().AsQueryable();
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

        if (!string.IsNullOrEmpty(status))
            query = query.Where(m => (m.IsBlocked ? "blocked" : "active") == status.ToLower());
        return Ok(await query.Select(ApiResponses.Master).ToListAsync());
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> GetAdminMasters()
    {
        var now = DateTimeOffset.UtcNow;
        var masters = await _context.Masters.AsNoTracking().Include(m => m.User).Include(m => m.Category)
            .Include(m => m.District).ThenInclude(d => d.City).Include(m => m.Subscriptions)
            .Include(m => m.Bookings).ThenInclude(b => b.Review).ToListAsync();
        return Ok(masters.Select(m =>
        {
            var paid = EffectivePlanResolver.Resolve(m.Subscriptions, now);
            return new MasterAdminDto
            {
                Id = m.Id, FirstName = m.User.FirstName, LastName = m.User.LastName,
                Category = m.Category?.Name ?? "", City = m.District?.City?.Name ?? "",
                Status = m.IsBlocked ? "blocked" : "active", SubscriptionUntil = paid?.Plan == SubscriptionPlan.Free ? null : paid?.ExpiresAt,
                Tariff = paid?.Plan.ToString().ToLowerInvariant() ?? "none", IsBlocked = m.IsBlocked,
                DistrictName = m.District?.Name ?? "", CreatedAt = m.User.CreatedAt, AvatarUrl = m.User.AvatarUrl,
                Slug = m.Slug, PhotoId = m.User.PhotoId,
                Rating = m.Bookings.Where(b => b.Review != null).Select(b => (double?)b.Review!.Rating).Average(),
                ClientsCount = m.Bookings.Where(b => b.Status == BookingStatus.Completed).Select(b => b.UserId).Distinct().Count()
            };
        }));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        var master = await _context.Masters.AsNoTracking().Where(m => m.Id == id)
            .Select(ApiResponses.Master).FirstOrDefaultAsync();
        return master == null ? NotFound(new { message = "Master not found" }) : Ok(master);
    }

    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var master = await _context.Masters.AsNoTracking().Where(m => m.Slug == slug)
            .Select(ApiResponses.Master).FirstOrDefaultAsync();
        return master == null ? NotFound(new { message = "Master not found" }) : Ok(master);
    }
    [HttpPatch("{id:int}/location")]
    [Authorize(Roles = "Master,Superadmin")]
    public async Task<IActionResult> UpdateLocation(int id, [FromBody] UpdateMasterLocationDto dto)
    {
        if (User.UserId() is not int userId) return Unauthorized();
        var isAdmin = User.IsInRole("Superadmin");
        if (!await _context.Masters.AsNoTracking().AnyAsync(m => m.Id == id && (isAdmin || m.UserId == userId)))
            return NotFound(new { message = "Master not found" });
        await using var transaction = await _masterLock.AcquireAsync(id, HttpContext.RequestAborted);
        var master = await _context.Masters
            .Include(m => m.District)
                .ThenInclude(d => d.City)
            .FirstOrDefaultAsync(m => m.Id == id && (isAdmin || m.UserId == userId));

        if (master == null) return NotFound(new { message = "Master not found" });

        if (dto.DistrictId.HasValue && !await _context.Districts.AnyAsync(d => d.Id == dto.DistrictId.Value))
            return NotFound(new { message = "District not found" });
        if (BookingMutationGuard.LocationChanges(master, dto.DistrictId ?? master.DistrictId,
            dto.Address, dto.Latitude, dto.Longitude) && await BookingMutationGuard
                .FutureActive(_context, master.Id, DateTimeOffset.UtcNow).AnyAsync())
            return Conflict(BookingMutationGuard.Conflict("location"));
        if (dto.DistrictId.HasValue)
        {
            master.DistrictId = dto.DistrictId.Value;
        }

        master.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
        master.Latitude = dto.Latitude;
        master.Longitude = dto.Longitude;

        await _context.SaveChangesAsync();
        await transaction.CommitAsync(HttpContext.RequestAborted);

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
    [ServiceFilter(typeof(ImageUploadFilter))]
    [EnableRateLimiting("uploads")]
    [Authorize(Roles ="Master,Superadmin")]
    public async Task<IActionResult> Create([FromForm] CreateMasterDto dto, [FromQuery] int? id)
    {
        var userid = User.FindFirstValue("userId");

        if (!int.TryParse(userid, out var UserId))
            return Unauthorized();

        var Role = User.FindFirstValue(ClaimTypes.Role);

        if (Role == null)
            return Unauthorized();

        if (Role.ToString() != "Master")
        {
            if (id == null) { return BadRequest(new { message = "id is null" }); }
            UserId = id.Value;
        }
        await using var transaction = await _userLock.AcquireAsync(UserId, HttpContext.RequestAborted);
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
        if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId)
            || !await _context.Districts.AnyAsync(d => d.Id == dto.DistrictId))
            return NotFound(new { message = "Category or District not found" });

        List<PortfolioPhoto> photos = new List<PortfolioPhoto>();
        await using var batch = new PhotoUploadBatch(_photoService,
            HttpContext.RequestServices.GetRequiredService<ILogger<MasterController>>());

        if (dto.portfolioPhotos != null && dto.portfolioPhotos.Any()) {

            if (dto.portfolioPhotos.Count > 10) { return BadRequest(new { message = "No more than 10 photos in portfolio" }); }
            foreach (var file in dto.portfolioPhotos)
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Corrupted file uploaded");

                var result = await batch.AddAsync(file);

                if (PhotoUploadBatch.Failed(result))
                {
                    return BadRequest(new
                    {
                        error = "image_upload_failed"
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
        await OnboardingService.SaveAndCompleteAsync(_context, master.Id);
        await transaction.CommitAsync(HttpContext.RequestAborted);
        batch.Commit();

        return CreatedAtAction(nameof(GetById), new { id = master.Id }, new { master.Id, master.Slug });
    }

    [HttpPut]
    [ServiceFilter(typeof(ImageUploadFilter))]
    [EnableRateLimiting("uploads")]
    [Authorize(Roles = "Master,Superadmin")]
    public async Task<IActionResult> Update( [FromForm] CreateMasterDto dto, [FromQuery]int? id)
    {
        var userid = User.FindFirstValue("userId");

        if (!int.TryParse(userid, out var UserId))
            return Unauthorized();

        if (User.IsInRole("Superadmin"))
        {
            if (id == null) { return BadRequest(new { message = "id is null" }); }
            UserId = id.Value;
        }

        var masterId = await _context.Masters.AsNoTracking().Where(m => m.UserId == UserId)
            .Select(m => (int?)m.Id).SingleOrDefaultAsync();
        if (masterId == null) return NotFound(new { message = "Master not found" });
        await using var transaction = await _masterLock.AcquireAsync(masterId.Value, HttpContext.RequestAborted);
        var master = await _context.Masters
             .Include(m => m.PortfolioPhotos)
             .FirstOrDefaultAsync(m => m.Id == masterId.Value);

        if (master == null) return NotFound(new { message = "Master not found" });
        if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId)
            || !await _context.Districts.AnyAsync(d => d.Id == dto.DistrictId))
            return NotFound(new { message = "Category or District not found" });

        if (master.CategoryId != dto.CategoryId && await BookingMutationGuard
            .CategoryConflictsAsync(_context, master.Id, dto.CategoryId, HttpContext.RequestAborted))
            return Conflict(new { error = "dependent_services_conflict",
                message = "Update dependent Service Groups before changing the Master's category." });
        if (master.SlotStepMin != dto.SlotStepMin && await BookingMutationGuard
            .StepConflictsAsync(_context, master.Id, dto.SlotStepMin, HttpContext.RequestAborted))
            return Conflict(BookingMutationGuard.Conflict("slot step"));
        if (BookingMutationGuard.LocationChanges(master, dto.DistrictId, dto.Address, dto.Latitude, dto.Longitude)
            && await BookingMutationGuard.FutureActive(_context, master.Id, DateTimeOffset.UtcNow).AnyAsync())
            return Conflict(BookingMutationGuard.Conflict("location"));

        List<PortfolioPhoto> photos = new List<PortfolioPhoto>();
        await using var batch = new PhotoUploadBatch(_photoService,
            HttpContext.RequestServices.GetRequiredService<ILogger<MasterController>>());

        if (dto.portfolioPhotos != null && dto.portfolioPhotos.Any())
        {
            if (dto.portfolioPhotos.Count > (10-master.PortfolioPhotos.Count)) { return BadRequest(new { message = "No more than 10 photos in portfolio" }); }

            foreach (var file in dto.portfolioPhotos)
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Corrupted file uploaded");

                var result = await batch.AddAsync(file);

                if (PhotoUploadBatch.Failed(result))
                {
                    return BadRequest(new
                    {
                        error = "image_upload_failed"
                    });
                }

                PortfolioPhoto photo = new PortfolioPhoto
                {
                    PhotoId = result.PublicId,
                    PhotoUrl = result.SecureUrl.AbsoluteUri,

                };


                photos.Add(photo);



                

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

        await OnboardingService.SaveAndCompleteAsync(_context, master.Id);
        await transaction.CommitAsync(HttpContext.RequestAborted);
        batch.Commit();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await _context.EntitlementGrants.AnyAsync(g => g.MasterId == id))
            return Conflict(new { error = "billing_history_retention" });
        var master = await _context.Masters.FindAsync(id);
        if (master == null) return NotFound(new { message = "Master not found" });

        _context.Masters.Remove(master);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Master deleted successfully" });
    }

    [HttpDelete("portfolio-photo/{id:int}")]
    [Authorize(Roles = "Master")]
    public async Task<ActionResult> DeletePhoto(int id) 
    {
        var userid = User.FindFirstValue("userId");

        if (!int.TryParse(userid, out var UserId))
            return Unauthorized();

        var masterId = await _context.Masters.AsNoTracking().Where(m => m.UserId == UserId)
            .Select(m => (int?)m.Id).SingleOrDefaultAsync();
        if (masterId == null) return NotFound(new { message = "Master Not Found" });
        await using var transaction = await _masterLock.AcquireAsync(masterId.Value, HttpContext.RequestAborted);
        var master = await _context.Masters.Include(m=>m.PortfolioPhotos).FirstOrDefaultAsync(m => m.Id == masterId.Value);

        if (master == null) return NotFound(new { message = "Master Not Found" });

        var photo = master.PortfolioPhotos.FirstOrDefault(p=>p.Id == id);

        if (photo == null) return NotFound(new { message = "Photo not found." });

        _context.PortfolioPhotos.Remove(photo);
        master.PortfolioPhotos.Remove(photo);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync(HttpContext.RequestAborted);
        await PhotoUploadBatch.DeleteBestEffortAsync(_photoService, photo.PhotoId,
            HttpContext.RequestServices.GetRequiredService<ILogger<MasterController>>());

        return Ok(new { message="Photo deleted."});


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
        if (plan.HasValue && !Enum.IsDefined(plan.Value)) return BadRequest("Invalid plan");
        if (days.HasValue && days.Value <= 0) return BadRequest("Wrong days value");
        if (plan == null && days == null) return BadRequest("Empty values - days, plan");
        if (!await _context.Masters.AnyAsync(m => m.Id == id)) return NotFound(new { message = "Master not found" });
        await using var billingTransaction = await _masterLock.AcquireAsync(id, HttpContext.RequestAborted);
        if (await _context.EntitlementGrants.AnyAsync(g => g.MasterId == id))
            return Conflict(new { error = "managed_entitlement", message = "Use the payment/refund flow for managed billing periods." });
        if (plan == null && days == null) { return BadRequest("Empty values - days, plan"); }
        var m = await _context.Masters.Include(m=>m.Subscriptions).FirstOrDefaultAsync(m=>m.Id==id);
        if (m == null) return NotFound(new { message = "Master not found" });

        var activeSub = EffectivePlanResolver.Resolve(m.Subscriptions, DateTimeOffset.UtcNow);
        if (activeSub?.Plan == SubscriptionPlan.Free) activeSub = null;

        if (days.HasValue)
        {
            try { _ = (activeSub?.ExpiresAt ?? DateTimeOffset.UtcNow).AddDays(days.Value); }
            catch (ArgumentOutOfRangeException) { return BadRequest("Subscription expiry is outside the supported date range"); }
        }

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
            await billingTransaction.CommitAsync(HttpContext.RequestAborted);

            return Ok(new { text = "Ok", Plan = nSub.Plan, ExpiresAt = nSub.ExpiresAt });
        }

        if (activeSub == null) return BadRequest("No active paid subscription");
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
            await billingTransaction.CommitAsync(HttpContext.RequestAborted);
            return Ok(new { text = "Ok", Plan = activeSub.Plan, ExpiresAt = activeSub.ExpiresAt });
        }


        await _context.SaveChangesAsync();
            await billingTransaction.CommitAsync(HttpContext.RequestAborted);
        return Ok(new { text = "Ok", Plan = nSub.Plan, ExpiresAt = nSub.ExpiresAt });
    }

    [HttpPost("test-seed/expire-in-10m")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> CreateTestMasterWith10mSub()
    {
        if (!HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment()) return NotFound();
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
