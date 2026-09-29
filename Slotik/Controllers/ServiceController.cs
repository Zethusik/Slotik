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
        [Authorize(Roles = "Superadmin")]
        public async Task<ActionResult> Create([FromBody] CreateServiceDTO dto)
        {
            var maxSortOrder = await _context.Services
                .Where(s =>
                    s.MasterId == dto.MasterId &&
                    s.GroupId == dto.GroupId)
                .MaxAsync(s => (int?)s.SortOrder) ?? 0;

            Service sub = new Service
            {
                MasterId = dto.MasterId,
                DurationMin = dto.DurationMin,
                Price = dto.Price,
                Name = dto.Name,
                GroupId = dto.GroupId,
                IsPopular  = dto.IsPopular,
                SortOrder = maxSortOrder+1,
            };

            _context.Services.Add(sub);
            await _context.SaveChangesAsync();
            return Ok(sub);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Superadmin")]
        public async Task<ActionResult> Update(int id, [FromBody] CreateServiceDTO dto)
        {
            var serviceToChange = await _context.Services.FirstOrDefaultAsync(s => s.Id == id);
            if (serviceToChange == null) { return NotFound("Service Not Found."); }

            serviceToChange.MasterId = dto.MasterId;
            serviceToChange.DurationMin = dto.DurationMin;
            serviceToChange.Price = dto.Price;
            serviceToChange.Name = dto.Name;
            serviceToChange.SortOrder = dto.SortOrder;
            serviceToChange.GroupId = dto.GroupId;
            serviceToChange.IsPopular = dto.IsPopular;

            await _context.SaveChangesAsync();

            return Ok(serviceToChange);
        }
    }
}