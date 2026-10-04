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

        public ServicePhotoController(AppDbContext context,IPhotoService photoService)
        {
            _context = context;
            _photoService = photoService;
        }

        // GET /api/ServicePhoto?masterId=10
        [HttpGet]
        public async Task<ActionResult> GetAll([FromQuery] int? masterId)
        {
            var query = _context.ServicePhotos
                .Include(s => s.Service)
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
            var sphoto = await _context.ServicePhotos.Include(s => s.Service).FirstOrDefaultAsync(s => s.Id == id);
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
            var master = await _context.Masters.FirstOrDefaultAsync(m => m.UserId == UserId);

            if (master == null) { return NotFound(new { message="Master profile Not found."}); }

            var sphoto = await _context.ServicePhotos.Include(s=>s.Service).FirstOrDefaultAsync(s => s.Id == id);
            if (sphoto == null) { return NotFound(new { message="Photo not found"}); }

            if (sphoto.Service == null) { return BadRequest(new { message="Photo is not related to any service."}); }

            if (sphoto.Service.MasterId != master.Id) { return Forbid(); }


            var remResult = await _photoService.DeletePhotoAsync(sphoto.photoId);

            if (remResult.Error != null) { return BadRequest(new { message = remResult.Error.Message }); }

            _context.ServicePhotos.Remove(sphoto);
            await _context.SaveChangesAsync();

            return Ok("Service Photo is deleted");
        }

        [HttpPost]
        [Authorize(Roles = "Superadmin")]
        public async Task<ActionResult> Create([FromBody] CreateServicePhotoDTO dto)
        {
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

            sphotoToChange.ServiceId = dto.ServiceId;
            sphotoToChange.PhotoUrl = dto.PhotoUrl;

            await _context.SaveChangesAsync();

            return Ok(sphotoToChange);
        }
    }
}
