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
    public class ServiceController : ControllerBase
    {

        private readonly AppDbContext _context;

        public ServiceController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult> GetAll()
        {
            var service = await _context.Services.Include(s => s.Master).Include(s=>s.Photos).Include(s=>s.Bookings).ToListAsync();
            return Ok(service);
        }

        [HttpGet("{Id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            var service = await _context.Services.Include(s => s.Master).Include(s => s.Photos).Include(s => s.Bookings).FirstOrDefaultAsync(s => s.Id == id);
            if (service == null) { return NotFound("Service Not Found."); }

            return Ok(service);
        }

        [HttpDelete("{Id}")]
        [Authorize]

        public async Task<ActionResult> DeleteById(int Id)
        {
            var service = await _context.Services.Include(s => s.Master).Include(s => s.Photos).Include(s => s.Bookings).FirstOrDefaultAsync(s => s.Id == Id);

            if (service == null) { return NotFound("Service Not Found."); }

            _context.Services.Remove(service);
            _context.SaveChanges();

            return Ok("Service is deleted");
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> Create([FromBody] CreateServiceDTO dto)
        {
            Service sub = new Service
            {
                MasterId = dto.MasterId,
                DurationMin = dto.DurationMin,
                Price = dto.Price,
                Name = dto.Name,

            };

            _context.Services.Add(sub);
            await _context.SaveChangesAsync();
            return Ok(sub);
        }

        [HttpPut("{id}")]
        [Authorize]

        public async Task<ActionResult> Update(int id, [FromBody] CreateServiceDTO dto)
        {
            var serviceToChange = await _context.Services.FirstOrDefaultAsync(s => s.Id == id);
            if (serviceToChange == null) { return NotFound("Service Not Found."); }

            serviceToChange.MasterId = dto.MasterId;
            serviceToChange.DurationMin = dto.DurationMin;
            serviceToChange.Price = dto.Price;
            serviceToChange.Name = dto.Name;

            await _context.SaveChangesAsync();

            return Ok(serviceToChange);
        }
    }
}
