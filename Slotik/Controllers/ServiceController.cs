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

        public ServiceController(AppDbContext context, IPhotoService photoService)
        {
            _context = context;
            _photoService = photoService;
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

        [HttpGet("{Id}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetById(int id)
        {
            var service = await _context.Services
                .Include(s => s.Master)
                .Include(s => s.Photos)
                .FirstOrDefaultAsync(s => s.Id == id);

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
        [Authorize(Roles = "Master")]
        public async Task<ActionResult> Create([FromForm] CreateServiceDTO dto)
        {

            var userid = User.FindFirstValue("userId");

            if (!int.TryParse(userid, out var UserId))
                return Unauthorized();

            var master = await _context.Masters.FirstOrDefaultAsync(m => m.UserId == UserId);

            if (master == null) { return NotFound(new { message = "Not found Master profile." }); }

            var maxSortOrder = await _context.Services
                .Where(s =>
                    s.MasterId == master.Id &&
                    s.GroupId == dto.GroupId)
                .MaxAsync(s => (int?)s.SortOrder) ?? 0;

            

            if (!dto.files.Any() || dto.files == null || dto.files.Count() > 3) { return BadRequest(new { message = "No Photos or more than 3" }); }

            

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

            foreach (var file in dto.files)
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

                ServicePhoto photo = new ServicePhoto { 
                photoId = result.PublicId,
                PhotoUrl = result.SecureUrl.AbsoluteUri,
                ServiceId = sub.Id,
                SortOrder = sortorder,
                };
                
                sortorder++;
                sub.Photos.Add(photo);
                
            }
            await _context.SaveChangesAsync();
            return Ok(sub);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Master")]
        public async Task<ActionResult> Update(int id, [FromBody] CreateServiceDTO dto)
        {
            var serviceToChange = await _context.Services.Include(s=>s.Photos).FirstOrDefaultAsync(s => s.Id == id);
            if (serviceToChange == null) { return NotFound("Service Not Found."); }

            serviceToChange.MasterId = dto.MasterId;
            serviceToChange.DurationMin = dto.DurationMin;
            serviceToChange.Price = dto.Price;
            serviceToChange.Name = dto.Name;
            serviceToChange.SortOrder = dto.SortOrder;
            serviceToChange.GroupId = dto.GroupId;
            serviceToChange.IsPopular = dto.IsPopular;

            if (serviceToChange.Photos != null && serviceToChange.Photos.Any())
            {
                foreach (var photo in serviceToChange.Photos)
                {
                    await _photoService.DeletePhotoAsync(photo.photoId);
                    _context.ServicePhotos.Remove(photo);

                }
            }

            List<ServicePhoto> newPhotos = new List<ServicePhoto>();
            var sortorder = 1;

            foreach (var file in dto.files)
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

                ServicePhoto photo = new ServicePhoto
                {
                    photoId = result.PublicId,
                    PhotoUrl = result.SecureUrl.AbsoluteUri,
                    ServiceId = serviceToChange.Id,
                    SortOrder = sortorder,
                };

                sortorder++;
                newPhotos.Add(photo);

            }
            serviceToChange.Photos = newPhotos;

            await _context.SaveChangesAsync();

            return Ok(serviceToChange);
        }
    }
}