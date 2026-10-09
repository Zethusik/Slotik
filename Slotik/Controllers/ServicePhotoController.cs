using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    public class ServicePhotoController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPhotoService _photoService;
        private readonly IMasterSubscriptionLock _masterLock;

        public ServicePhotoController(AppDbContext context,IPhotoService photoService, IMasterSubscriptionLock masterLock)
        {
            _context = context;
            _photoService = photoService;
            _masterLock = masterLock;
        }

        // GET /api/ServicePhoto?masterId=10
        [HttpGet]
        public async Task<ActionResult> GetAll([FromQuery] int? masterId)
        {
            var query = _context.ServicePhotos
                .AsQueryable();

            if (masterId.HasValue)
            {
                query = query.Where(p => p.Service.MasterId == masterId.Value);
            }

            var photos = await query.Select(p => new
            {
                id = p.Id,
                serviceId = p.ServiceId,
                photoUrl = p.PhotoUrl,
                sortOrder = 0
            }).ToListAsync();

            return Ok(photos);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult> GetById(int id)
        {
            var sphoto = await _context.ServicePhotos.Select(s => new { s.Id, s.ServiceId, s.PhotoUrl, s.SortOrder }).FirstOrDefaultAsync(s => s.Id == id);
            if (sphoto == null) { return NotFound("Service Photo Not Found."); }

            return Ok(sphoto);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Master")]
        public async Task<ActionResult> DeleteById(int id)
        {
            var userid = User.FindFirstValue("userId");

            if (!int.TryParse(userid, out var UserId))
                return Unauthorized();
            var masterId = await _context.Masters.AsNoTracking().Where(m => m.UserId == UserId)
                .Select(m => (int?)m.Id).SingleOrDefaultAsync();
            if (masterId == null) return NotFound(new { message = "Master profile Not found." });
            await using var transaction = await _masterLock.AcquireAsync(masterId.Value, HttpContext.RequestAborted);
            var master = await _context.Masters.FirstOrDefaultAsync(m => m.Id == masterId.Value);

            if (master == null) { return NotFound(new { message="Master profile Not found."}); }

            var sphoto = await _context.ServicePhotos.Include(s=>s.Service).FirstOrDefaultAsync(s => s.Id == id);
            if (sphoto == null) { return NotFound(new { message="Photo not found"}); }

            if (sphoto.Service == null) { return BadRequest(new { message="Photo is not related to any service."}); }

            if (sphoto.Service.MasterId != master.Id) { return Forbid(); }


            _context.ServicePhotos.Remove(sphoto);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync(HttpContext.RequestAborted);
            await PhotoUploadBatch.DeleteBestEffortAsync(_photoService, sphoto.photoId,
                HttpContext.RequestServices.GetRequiredService<ILogger<ServicePhotoController>>());

            return Ok("Service Photo is deleted");
        }

        [HttpPost]
        [Authorize(Roles = "Superadmin")]
        public async Task<ActionResult> Create([FromBody] CreateServicePhotoDTO dto)
        {
            if (!await _context.Services.AnyAsync(s => s.Id == dto.ServiceId)) return NotFound("Service Not Found.");
            ServicePhoto sub = new ServicePhoto
            {
                ServiceId = dto.ServiceId,
                PhotoUrl = dto.PhotoUrl,
            };

            _context.ServicePhotos.Add(sub);
            await _context.SaveChangesAsync();
            return Ok(sub);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Superadmin")]
        public async Task<ActionResult> Update(int id, [FromBody] CreateServicePhotoDTO dto)
        {
            var sphotoToChange = await _context.ServicePhotos.FirstOrDefaultAsync(s => s.Id == id);
            if (sphotoToChange == null) { return NotFound("Service Photo Not Found."); }
            if (!await _context.Services.AnyAsync(s => s.Id == dto.ServiceId)) return NotFound("Service Not Found.");

            sphotoToChange.ServiceId = dto.ServiceId;
            sphotoToChange.PhotoUrl = dto.PhotoUrl;

            await _context.SaveChangesAsync();

            return Ok(sphotoToChange);
        }
    }
}
