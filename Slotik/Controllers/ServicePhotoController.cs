using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;

namespace Slotik.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServicePhotoController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ServicePhotoController (AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult> GetAll()
        {
            var sphoto = await _context.ServicePhotos.Include(s => s.Service).ToListAsync();
            return Ok(sphoto);
        }

        [HttpGet("{Id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            var sphoto = await  _context.ServicePhotos.Include(s => s.Service).FirstOrDefaultAsync(s => s.Id == id);
            if (sphoto == null) { return NotFound("Service Photo Not Found."); }

            return Ok(sphoto);
        }

        [HttpDelete("{Id}")]
        [Authorize]

        public async Task<ActionResult> DeleteById(int Id)
        {
            var sphoto = await _context.ServicePhotos.FirstOrDefaultAsync(s => s.Id == Id);

            if (sphoto == null) { return NotFound("Service Photo Not Found."); }

            _context.ServicePhotos.Remove(sphoto);
            _context.SaveChanges();

            return Ok("Service Photo is deleted");
        }

        [HttpPost]
        [Authorize]
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

        [HttpPut("{id}")]
        [Authorize]

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
