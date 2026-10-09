using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Services;
using System.Security.Claims;

namespace Slotik.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPhotoService _photoService;
        private readonly IMasterSubscriptionLock _masterLock;

        public ServiceController(AppDbContext context, IPhotoService photoService, IMasterSubscriptionLock masterLock)
        {
            _context = context;
            _photoService = photoService;
            _masterLock = masterLock;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> GetAll([FromQuery] int? masterId)
        {
            var query = _context.Services.AsQueryable();

            if (masterId.HasValue)
            {
                query = query.Include(s=>s.Group).Where(s => s.MasterId == masterId.Value);
            }

            var services = await query
                .Select(s => new
                {
                    id = s.Id,
                    masterId = s.MasterId,
                    name = s.Name,
                    price = s.Price,
                    durationMin = s.DurationMin,
                    description = s.Description,
                    included = s.Included,
                    groupId = s.GroupId,
                    groupName = s.Group != null ? s.Group.Name : null,
                    isPopular = s.IsPopular,
                    sortOrder = s.SortOrder,
                })
                .ToListAsync();

            return Ok(services);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetById(int id)
        {
            var service = await _context.Services
                .Where(s => s.Id == id)
                .Select(ApiResponses.Service)
                .FirstOrDefaultAsync();

            if (service == null) { return NotFound("Service Not Found."); }

            return Ok(service);
        }

        [HttpDelete("{Id}")]
        [Authorize(Roles = "Superadmin")]
        public async Task<ActionResult> DeleteById(int Id)
        {
            var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == Id);

            if (service == null) { return NotFound("Service Not Found."); }

            _context.Services.Remove(service);
            await _context.SaveChangesAsync();

            return Ok("Service is deleted");
        }

        [HttpPost]
        [ServiceFilter(typeof(ImageUploadFilter))]
    [EnableRateLimiting("uploads")]
        [Authorize(Roles = "Master")]
        public async Task<ActionResult> Create([FromForm] CreateServiceDTO dto)
        {

            var userid = User.FindFirstValue("userId");

            if (!int.TryParse(userid, out var UserId))
                return Unauthorized();

            var masterId = await _context.Masters.AsNoTracking().Where(m => m.UserId == UserId)
                .Select(m => (int?)m.Id).SingleOrDefaultAsync();
            if (masterId == null) return NotFound(new { message = "Not found Master profile." });
            await using var transaction = await _masterLock.AcquireAsync(masterId.Value, HttpContext.RequestAborted);
            var master = await _context.Masters.FirstOrDefaultAsync(m => m.Id == masterId.Value);

            if (master == null) { return NotFound(new { message = "Not found Master profile." }); }
            if (dto.GroupId.HasValue && !await _context.ServiceGroups.AnyAsync(g => g.Id == dto.GroupId && g.CategoryId == master.CategoryId))
                return BadRequest(new { error = "invalid_service_group", message = "Group must belong to the Master's category." });

            var maxSortOrder = await _context.Services
                .Where(s =>
                    s.MasterId == master.Id &&
                    s.GroupId == dto.GroupId)
                .MaxAsync(s => (int?)s.SortOrder) ?? 0;

            

            if (dto.files == null || !dto.files.Any() || dto.files.Count > 3) { return BadRequest(new { message = "No Photos or more than 3" }); }

            

            Service sub = new Service
            {
                MasterId = master.Id,
                DurationMin = dto.DurationMin,
                Price = dto.Price,
                Name = dto.Name,
                GroupId = dto.GroupId,
                IsPopular  = dto.IsPopular,
                SortOrder = maxSortOrder+1,
            };

           
           

           
            int sortorder = 1;
            await using var batch = new PhotoUploadBatch(_photoService,
                HttpContext.RequestServices.GetRequiredService<ILogger<ServiceController>>());

            foreach (var file in dto.files)
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

                ServicePhoto photo = new ServicePhoto { 
                photoId = result.PublicId,
                PhotoUrl = result.SecureUrl.AbsoluteUri,
                ServiceId = sub.Id,
                SortOrder = sortorder,
                };
                
                sortorder++;
                sub.Photos.Add(photo);
                
            }
            _context.Services.Add(sub);
            await OnboardingService.SaveAndCompleteAsync(_context, master.Id);
            await transaction.CommitAsync(HttpContext.RequestAborted);
            batch.Commit();
            return Ok(await _context.Services.Where(s => s.Id == sub.Id).Select(ApiResponses.Service).SingleAsync());
        }

        [HttpPut("{id}")]
        [ServiceFilter(typeof(ImageUploadFilter))]
    [EnableRateLimiting("uploads")]
        [Authorize(Roles = "Master")]
        public async Task<ActionResult> Update(int id, [FromForm] CreateServiceDTO dto)
        {
            var userid = User.FindFirstValue("userId");
            if (!int.TryParse(userid, out var UserId))
                return Unauthorized();

            var masterId = await _context.Masters.AsNoTracking().Where(m => m.UserId == UserId)
                .Select(m => (int?)m.Id).SingleOrDefaultAsync();
            if (masterId == null) return NotFound(new { message = "Not found Master profile." });
            await using var transaction = await _masterLock.AcquireAsync(masterId.Value, HttpContext.RequestAborted);
            var master = await _context.Masters.FirstOrDefaultAsync(m => m.Id == masterId.Value);
            if (master == null) { return BadRequest(new { message="User does not have master profile."}); }

            var serviceToChange = await _context.Services.Include(s=>s.Photos).FirstOrDefaultAsync(s => s.Id == id);
            if (serviceToChange == null) { return NotFound("Service Not Found."); }
            if (serviceToChange.MasterId != master.Id) { return Forbid(); }
            // Approved policy keeps the promised StartsAt/EndsAt unchanged.
            if (serviceToChange.DurationMin != dto.DurationMin && await BookingMutationGuard
                .FutureActive(_context, master.Id, DateTimeOffset.UtcNow).AnyAsync(b => b.ServiceId == id))
                return Conflict(BookingMutationGuard.Conflict("service duration"));
            if (dto.GroupId.HasValue && !await _context.ServiceGroups.AnyAsync(g => g.Id == dto.GroupId && g.CategoryId == master.CategoryId))
                return BadRequest(new { error = "invalid_service_group", message = "Group must belong to the Master's category." });

            
            serviceToChange.DurationMin = dto.DurationMin;
            serviceToChange.Price = dto.Price;
            serviceToChange.Name = dto.Name;
            serviceToChange.SortOrder = dto.SortOrder;
            serviceToChange.GroupId = dto.GroupId;
            serviceToChange.IsPopular = dto.IsPopular;





            var sortorder = serviceToChange.Photos.Any()
                ? serviceToChange.Photos.Max(p => p.SortOrder) + 1
                : 1;
            await using var batch = new PhotoUploadBatch(_photoService,
                HttpContext.RequestServices.GetRequiredService<ILogger<ServiceController>>());
            if (dto.files != null && dto.files.Any()  )
            {
                if (serviceToChange.Photos.Count() + dto.files.Count() > 3) { return BadRequest(new { message = "Service must have only 3 photos" }); }
                foreach (var file in dto.files)
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

                    ServicePhoto photo = new ServicePhoto
                    {
                        photoId = result.PublicId,
                        PhotoUrl = result.SecureUrl.AbsoluteUri,
                        ServiceId = serviceToChange.Id,
                        SortOrder = sortorder,
                    };

                    sortorder++;
                   serviceToChange.Photos.Add(photo);

                }
            }
            

            await OnboardingService.SaveAndCompleteAsync(_context, master.Id);
            await transaction.CommitAsync(HttpContext.RequestAborted);
            batch.Commit();

            return Ok(await _context.Services.Where(s => s.Id == serviceToChange.Id).Select(ApiResponses.Service).SingleAsync());
        }
        //[HttpDelete("{id:int}")]
        //[Authorize(Roles = "Master")]
        //public async Task<ActionResult> deletePhoto(int id)
        //{
        //    var userid = User.FindFirstValue("userId");

        //    if (!int.TryParse(userid, out var UserId))
        //        return Unauthorized();

        //    var master = await _context.Masters.FirstOrDefaultAsync(m => m.UserId == UserId);
        //    if (master == null) { return NotFound(new { message = "User does not have master profile." }); }

        //    var photo = await _context.ServicePhotos.Include(sp => sp.Service).FirstOrDefaultAsync(sp => sp.Id == id);

        //    if (photo == null) { return NotFound(new { message = "Service photo Not found." }); }

        //    if (photo.Service.MasterId != master.Id) { return Forbid(); }

        //    var remResult = await _photoService.DeletePhotoAsync(photo.photoId);

        //    if (remResult.Error != null) { return BadRequest(new { message = remResult.Error.Message }); }
        //    _context.ServicePhotos.Remove(photo);


        //    await _context.SaveChangesAsync();

        //    return Ok(new { message = "Photo deleted." });
        //}
    }
}
